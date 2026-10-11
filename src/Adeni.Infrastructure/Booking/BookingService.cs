namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Caching;
using Adeni.Application.Common;
using Adeni.Application.Events;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Booking;
using Adeni.Domain.Booking.Events;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class BookingService(
    AdeniDbContext dbContext,
    IAvailabilityService availabilityService,
    IDistributedLockProvider lockProvider,
    Application.Reviews.IReviewService reviewService,
    IDomainEventCollector domainEventCollector,
    IEntitlementsService entitlementsService) : IBookingService
{
    public async Task<Result<BookingResponse>> CreateAsync(
        string customerAuth0Sub,
        CreateBookingRequest request,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<BookingResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        var normalizedIdempotencyKey = IdempotencyKeyNormalizer.Normalize(idempotencyKey);

        if (request.StartAt <= DateTimeOffset.UtcNow)
        {
            return Result.Failure<BookingResponse>(ErrorCodes.SlotExpiredError());
        }

        var guestCount = request.GuestCount <= 0 ? 1 : request.GuestCount;
        if (guestCount > ErrorCodes.MaxBookingGuests)
        {
            return Result.Failure<BookingResponse>(ErrorCodes.GuestLimitError());
        }

        var profile = await dbContext.BusinessProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == request.TenantId, cancellationToken);

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == request.TenantId && t.Status == TenantStatus.Verified,
                cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<BookingResponse>(Error.NotFound("Business"));
        }

        if (profile?.PublicPageShowBook == false)
        {
            return Result.Failure<BookingResponse>(ErrorCodes.BookingClosedError());
        }

        var entitlementCheck = await entitlementsService.EnsureCanCreateBookingAsync(
            request.TenantId,
            tenant.SubscriptionTier,
            cancellationToken);
        if (entitlementCheck.IsFailure)
        {
            return Result.Failure<BookingResponse>(entitlementCheck.Error);
        }

        var lineRequests = ResolveLineRequests(request);
        if (lineRequests.Count == 0)
        {
            return Result.Failure<BookingResponse>(ErrorCodes.CartEmptyError());
        }

        if (lineRequests.Count > ErrorCodes.MaxBookingLines)
        {
            return Result.Failure<BookingResponse>(
                Error.Validation($"A booking can include at most {ErrorCodes.MaxBookingLines} services."));
        }

        var serviceIds = lineRequests.Select(x => x.ServiceOfferingId).Distinct().ToArray();
        var services = await dbContext.ServiceOfferings
            .AsNoTracking()
            .Where(x => x.TenantId == request.TenantId && x.IsActive && serviceIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (services.Count != serviceIds.Length)
        {
            return Result.Failure<BookingResponse>(Error.NotFound("Service"));
        }

        var byId = services.ToDictionary(x => x.Id);
        var orderedServices = lineRequests.Select(line => byId[line.ServiceOfferingId]).ToArray();
        if (orderedServices.All(x => x.IsAddOn))
        {
            return Result.Failure<BookingResponse>(ErrorCodes.AddonRequiresParentError());
        }

        var primary = orderedServices.FirstOrDefault(x => !x.IsAddOn) ?? orderedServices[0];
        var baseDuration = orderedServices.Sum(x => x.DurationMinutes);
        var totalDuration = baseDuration * guestCount;
        if (totalDuration is < 5 or > 480 * ErrorCodes.MaxBookingGuests)
        {
            return Result.Failure<BookingResponse>(
                Error.Validation("Total appointment duration is out of range."));
        }

        string? staffDisplayName = null;
        if (request.StaffMemberId is Guid staffId)
        {
            var staff = await dbContext.StaffMembers
                .AsNoTracking()
                .Include(x => x.ServiceLinks)
                .FirstOrDefaultAsync(
                    x => x.Id == staffId && x.TenantId == request.TenantId && x.IsActive,
                    cancellationToken);

            if (staff is null)
            {
                return Result.Failure<BookingResponse>(ErrorCodes.StaffUnavailableError());
            }

            foreach (var svc in orderedServices)
            {
                var eligible = staff.ServiceLinks.Count == 0
                    || staff.ServiceLinks.Any(link => link.ServiceOfferingId == svc.Id);
                if (!eligible)
                {
                    return Result.Failure<BookingResponse>(ErrorCodes.StaffNotEligibleError());
                }
            }

            staffDisplayName = staff.DisplayName;
        }

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Auth0Sub == customerAuth0Sub, cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                Auth0Sub = customerAuth0Sub,
                Name = string.Empty,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Customers.Add(customer);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (normalizedIdempotencyKey is not null)
        {
            var existing = await dbContext.Bookings
                .AsNoTracking()
                .Include(x => x.Lines)
                .Include(x => x.Guests)
                .FirstOrDefaultAsync(
                    x => x.CustomerId == customer.Id && x.IdempotencyKey == normalizedIdempotencyKey,
                    cancellationToken);

            if (existing is not null)
            {
                var existingName = existing.Lines.Count > 0
                    ? existing.Lines.OrderBy(x => x.SortOrder).First().ServiceName
                    : await dbContext.ServiceOfferings.AsNoTracking()
                        .Where(x => x.Id == existing.ServiceOfferingId)
                        .Select(x => x.Name)
                        .FirstOrDefaultAsync(cancellationToken) ?? "Service";
                var existingStaffName = existing.StaffMemberId is Guid existingStaffId
                    ? await dbContext.StaffMembers.AsNoTracking()
                        .Where(x => x.Id == existingStaffId)
                        .Select(x => x.DisplayName)
                        .FirstOrDefaultAsync(cancellationToken)
                    : null;
                return Result.Success(
                    BookingMapper.ToResponse(existing, existingName, existingStaffName, primary.Currency));
            }
        }

        var lockKey = CacheKeys.SlotLock(
            request.TenantId,
            request.StartAt.ToUniversalTime(),
            primary.Id,
            request.StaffMemberId,
            totalDuration);
        var slotLock = await lockProvider.TryAcquireAsync(lockKey, CacheTtl.SlotLock, cancellationToken);
        if (slotLock is null)
        {
            return Result.Failure<BookingResponse>(ErrorCodes.SlotLockedError());
        }

        await using (slotLock)
        {
            var additionalIds = orderedServices
                .Select(x => x.Id)
                .Where(id => id != primary.Id)
                .ToArray();
            var isAvailable = await availabilityService.IsSlotAvailableAsync(
                request.TenantId,
                primary.Id,
                request.StartAt.ToUniversalTime(),
                totalDuration,
                request.StaffMemberId,
                additionalIds,
                cancellationToken);

            if (!isAvailable)
            {
                if (request.StartAt <= DateTimeOffset.UtcNow)
                {
                    return Result.Failure<BookingResponse>(ErrorCodes.SlotExpiredError());
                }

                if (request.StaffMemberId is not null)
                {
                    return Result.Failure<BookingResponse>(ErrorCodes.StaffUnavailableError());
                }

                return Result.Failure<BookingResponse>(ErrorCodes.SlotUnavailableError());
            }

            var now = DateTimeOffset.UtcNow;
            var autoConfirm = profile?.AutoConfirmBookings == true;
            var startAtUtc = request.StartAt.ToUniversalTime();
            var booking = new BookingRecord
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                ServiceOfferingId = primary.Id,
                CustomerId = customer.Id,
                StaffMemberId = request.StaffMemberId,
                GuestCount = guestCount,
                StartAt = startAtUtc,
                EndAt = startAtUtc.AddMinutes(totalDuration),
                Status = autoConfirm ? BookingStatus.Confirmed : BookingStatus.Pending,
                CustomerNotes = string.IsNullOrWhiteSpace(request.CustomerNotes)
                    ? null
                    : request.CustomerNotes.Trim(),
                IdempotencyKey = normalizedIdempotencyKey,
                CreatedAt = now,
                UpdatedAt = now
            };

            for (var i = 0; i < orderedServices.Length; i++)
            {
                var svc = orderedServices[i];
                booking.Lines.Add(new BookingLine
                {
                    Id = Guid.NewGuid(),
                    BookingId = booking.Id,
                    TenantId = request.TenantId,
                    ServiceOfferingId = svc.Id,
                    SortOrder = i,
                    PriceAmount = svc.PriceAmount,
                    DurationMinutes = svc.DurationMinutes,
                    ServiceName = svc.Name,
                    IsAddOn = svc.IsAddOn,
                });
            }

            var guestNames = request.Guests ?? Array.Empty<CreateBookingGuestRequest>();
            for (var i = 0; i < guestNames.Count && i < guestCount; i++)
            {
                var name = guestNames[i].DisplayName?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                booking.Guests.Add(new BookingGuest
                {
                    Id = Guid.NewGuid(),
                    BookingId = booking.Id,
                    TenantId = request.TenantId,
                    DisplayName = name.Length > 120 ? name[..120] : name,
                    SortOrder = i,
                });
            }

            if (autoConfirm)
            {
                domainEventCollector.Add(new BookingConfirmed(
                    booking.Id,
                    booking.TenantId,
                    booking.CustomerId,
                    booking.StartAt,
                    now));
            }

            dbContext.Bookings.Add(booking);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(
                BookingMapper.ToResponse(booking, primary.Name, staffDisplayName, primary.Currency));
        }
    }

    public async Task<IReadOnlyList<BookingResponse>> ListForTenantAsync(
        Guid tenantId,
        Guid? staffMemberId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.Guests)
            .Where(x => x.TenantId == tenantId);

        if (staffMemberId is Guid scopedStaffId)
        {
            query = query.Where(x => x.StaffMemberId == scopedStaffId);
        }

        var bookings = await query
            .OrderBy(x => x.StartAt)
            .ToListAsync(cancellationToken);

        if (bookings.Count == 0)
        {
            return Array.Empty<BookingResponse>();
        }

        var serviceIds = bookings.Select(x => x.ServiceOfferingId).Distinct().ToArray();
        var staffIds = bookings
            .Where(x => x.StaffMemberId.HasValue)
            .Select(x => x.StaffMemberId!.Value)
            .Distinct()
            .ToArray();

        var services = await dbContext.ServiceOfferings.AsNoTracking()
            .Where(x => serviceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var staffNames = staffIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.StaffMembers.AsNoTracking()
                .Where(x => staffIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        return bookings
            .Select(booking =>
            {
                services.TryGetValue(booking.ServiceOfferingId, out var service);
                var name = booking.Lines.OrderBy(x => x.SortOrder).FirstOrDefault()?.ServiceName
                    ?? service?.Name
                    ?? "Service";
                string? staffName = null;
                if (booking.StaffMemberId is Guid sid)
                {
                    staffNames.TryGetValue(sid, out staffName);
                }

                return BookingMapper.ToResponse(booking, name, staffName, service?.Currency);
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<CustomerBookingResponse>> ListForCustomerAsync(
        string customerAuth0Sub,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Array.Empty<CustomerBookingResponse>();
        }

        var customer = await dbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Auth0Sub == customerAuth0Sub, cancellationToken);
        if (customer is null)
        {
            return Array.Empty<CustomerBookingResponse>();
        }

        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.Guests)
            .Where(x => x.CustomerId == customer.Id)
            .OrderByDescending(x => x.StartAt)
            .ToListAsync(cancellationToken);

        if (bookings.Count == 0)
        {
            return Array.Empty<CustomerBookingResponse>();
        }

        var tenantIds = bookings.Select(x => x.TenantId).Distinct().ToArray();
        var serviceIds = bookings.Select(x => x.ServiceOfferingId).Distinct().ToArray();
        var staffIds = bookings
            .Where(x => x.StaffMemberId.HasValue)
            .Select(x => x.StaffMemberId!.Value)
            .Distinct()
            .ToArray();

        var tenants = await dbContext.Tenants.AsNoTracking()
            .Where(x => tenantIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var services = await dbContext.ServiceOfferings.AsNoTracking()
            .Where(x => serviceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var staffNames = staffIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.StaffMembers.AsNoTracking()
                .Where(x => staffIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var slugRows = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => tenantIds.Contains(x.TenantId) && x.IsActive)
            .Select(x => new { x.TenantId, x.Slug, x.IsPrimary })
            .ToListAsync(cancellationToken);

        var slugByTenant = slugRows
            .GroupBy(x => x.TenantId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.IsPrimary).First().Slug);

        var bookingIds = bookings.Select(x => x.Id).ToArray();
        var reviewsByBooking = await reviewService.GetReviewsForBookingsAsync(bookingIds, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        return bookings
            .Select(booking =>
            {
                reviewsByBooking.TryGetValue(booking.Id, out var review);
                var hasReview = review is not null;
                var canReview = !hasReview
                    && booking.Status == BookingStatus.Confirmed
                    && booking.EndAt <= now;
                services.TryGetValue(booking.ServiceOfferingId, out var service);
                var name = booking.Lines.OrderBy(x => x.SortOrder).FirstOrDefault()?.ServiceName
                    ?? service?.Name
                    ?? "Service";
                string? staffName = null;
                if (booking.StaffMemberId is Guid sid)
                {
                    staffNames.TryGetValue(sid, out staffName);
                }

                return BookingMapper.ToCustomerResponse(
                    booking,
                    name,
                    tenants.GetValueOrDefault(booking.TenantId, string.Empty),
                    slugByTenant.GetValueOrDefault(booking.TenantId, string.Empty),
                    canReview,
                    hasReview,
                    review?.Rating,
                    staffName,
                    service?.Currency);
            })
            .ToArray();
    }

    public Task<Result<BookingResponse>> AcceptAsync(
        Guid tenantId,
        Guid bookingId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        UpdateStatusAsync(
            tenantId,
            bookingId,
            BookingStatus.Confirmed,
            null,
            requireStaffMemberId,
            cancellationToken);

    public Task<Result<BookingResponse>> RejectAsync(
        Guid tenantId,
        Guid bookingId,
        string? reason,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        UpdateStatusAsync(
            tenantId,
            bookingId,
            BookingStatus.Rejected,
            reason,
            requireStaffMemberId,
            cancellationToken);

    public async Task<Result<CustomerBookingResponse>> CancelAsync(
        string customerAuth0Sub,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<CustomerBookingResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        var booking = await dbContext.Bookings
            .Include(x => x.Lines)
            .Include(x => x.Guests)
            .FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure<CustomerBookingResponse>(Error.NotFound("Booking"));
        }

        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == booking.CustomerId, cancellationToken);

        if (customer is null || customer.Auth0Sub != customerAuth0Sub)
        {
            return Result.Failure<CustomerBookingResponse>(Error.NotFound("Booking"));
        }

        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
        {
            return Result.Failure<CustomerBookingResponse>(
                Error.Conflict("Only pending or confirmed bookings can be cancelled."));
        }

        if (booking.StartAt <= DateTimeOffset.UtcNow)
        {
            return Result.Failure<CustomerBookingResponse>(
                Error.Conflict("Past bookings cannot be cancelled."));
        }

        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTimeOffset.UtcNow;
        domainEventCollector.Add(new BookingCancelled(
            booking.Id,
            booking.TenantId,
            booking.CustomerId,
            booking.StartAt,
            DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync(cancellationToken);

        var service = await dbContext.ServiceOfferings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == booking.ServiceOfferingId, cancellationToken);
        var businessName = await dbContext.Tenants.AsNoTracking()
            .Where(x => x.Id == booking.TenantId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        var slug = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == booking.TenantId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary)
            .Select(x => x.Slug)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        var cancelStaffName = booking.StaffMemberId is Guid cancelStaffId
            ? await dbContext.StaffMembers.AsNoTracking()
                .Where(x => x.Id == cancelStaffId)
                .Select(x => x.DisplayName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var name = booking.Lines.OrderBy(x => x.SortOrder).FirstOrDefault()?.ServiceName
            ?? service?.Name
            ?? "Service";

        return Result.Success(BookingMapper.ToCustomerResponse(
            booking,
            name,
            businessName,
            slug,
            staffDisplayName: cancelStaffName,
            currency: service?.Currency));
    }

    private async Task<Result<BookingResponse>> UpdateStatusAsync(
        Guid tenantId,
        Guid bookingId,
        BookingStatus status,
        string? businessNotes,
        Guid? requireStaffMemberId,
        CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings
            .Include(x => x.Lines)
            .Include(x => x.Guests)
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.TenantId == tenantId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure<BookingResponse>(Error.NotFound("Booking"));
        }

        if (requireStaffMemberId is Guid requiredStaff
            && booking.StaffMemberId != requiredStaff)
        {
            return Result.Failure<BookingResponse>(ErrorCodes.PermissionDeniedError());
        }

        if (booking.Status != BookingStatus.Pending)
        {
            return Result.Failure<BookingResponse>(Error.Conflict("Only pending bookings can be updated."));
        }

        booking.Status = status;
        booking.BusinessNotes = string.IsNullOrWhiteSpace(businessNotes) ? null : businessNotes.Trim();
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        var now = DateTimeOffset.UtcNow;
        switch (status)
        {
            case BookingStatus.Confirmed:
                domainEventCollector.Add(new BookingConfirmed(
                    booking.Id,
                    booking.TenantId,
                    booking.CustomerId,
                    booking.StartAt,
                    now));
                break;
            case BookingStatus.Rejected:
                domainEventCollector.Add(new BookingRejected(
                    booking.Id,
                    booking.TenantId,
                    booking.CustomerId,
                    booking.BusinessNotes,
                    now));
                break;
        }

        var service = await dbContext.ServiceOfferings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == booking.ServiceOfferingId, cancellationToken);

        var staffName = booking.StaffMemberId is Guid staffId
            ? await dbContext.StaffMembers.AsNoTracking()
                .Where(x => x.Id == staffId)
                .Select(x => x.DisplayName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        await dbContext.SaveChangesAsync(cancellationToken);
        var name = booking.Lines.OrderBy(x => x.SortOrder).FirstOrDefault()?.ServiceName
            ?? service?.Name
            ?? "Service";
        return Result.Success(BookingMapper.ToResponse(booking, name, staffName, service?.Currency));
    }

    private static IReadOnlyList<CreateBookingLineRequest> ResolveLineRequests(CreateBookingRequest request)
    {
        if (request.Lines is { Count: > 0 })
        {
            return request.Lines;
        }

        if (request.ServiceOfferingId == Guid.Empty)
        {
            return Array.Empty<CreateBookingLineRequest>();
        }

        return [new CreateBookingLineRequest(request.ServiceOfferingId, request.StaffMemberId)];
    }
}
