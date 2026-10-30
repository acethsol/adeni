namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Abstractions;
using Adeni.Application.Booking;
using Adeni.Application.Caching;
using Adeni.Application.Common;
using Adeni.Application.Events;
using Adeni.Application.Finance;
using Adeni.Application.Security;
using Adeni.Application.Subscriptions;
using Adeni.Application.Realtime;
using Adeni.Application.Tenancy;
using Adeni.Infrastructure.Realtime;
using Adeni.Domain.Auditing;
using Adeni.Domain.Booking;
using Adeni.Domain.Booking.Events;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Finance;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class BookingService(
    AdeniDbContext dbContext,
    IAvailabilityService availabilityService,
    IDistributedLockProvider lockProvider,
    Application.Reviews.IReviewService reviewService,
    IDomainEventCollector domainEventCollector,
    IEntitlementsService entitlementsService,
    IPaymentOrchestrator paymentOrchestrator,
    IBillingDocumentService billingDocuments,
    IStaffCompensationService staffCompensation,
    ITenantCustomerService tenantCustomers,
    ITenantRealtimePublisher tenantRealtime,
    IAuditLogWriter auditLogWriter,
    ICorrelationContext correlationContext,
    ICurrentUser currentUser,
    IBookingIntakeAssignmentWriter? intake = null,
    Application.Retention.IBenefitRedemptionService? benefits = null,
    Application.Retention.ILoyaltyEarnService? loyaltyEarn = null,
    Application.Retention.ILoyaltyRedemptionService? loyaltyRedeem = null) : IBookingService
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

        var locationCheck = await TenantLocationScope.ValidateOptionalLocationIdAsync(
            dbContext,
            request.TenantId,
            request.BusinessLocationId,
            cancellationToken);
        if (locationCheck.IsFailure)
        {
            return Result.Failure<BookingResponse>(locationCheck.Error!);
        }

        var businessLocationId = locationCheck.Value;
        if (businessLocationId is null)
        {
            businessLocationId = await dbContext.BusinessLocations.AsNoTracking()
                .Where(x => x.TenantId == request.TenantId && x.IsActive)
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.CreatedAt)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var entitlementCheck = await entitlementsService.EnsureCanCreateBookingForTenantAsync(
            tenant,
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
                businessLocationId,
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
                BusinessLocationId = businessLocationId,
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

            if (intake is not null)
            {
                var required = await intake.PrepareAsync(request.TenantId, orderedServices.Select(x => x.Id).ToArray(), cancellationToken);
                if (required.IsFailure) return Result.Failure<BookingResponse>(required.Error);
                intake.Stage(request.TenantId, booking.Id, required.Value!, now);
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

            IReadOnlyList<Application.Retention.ReservedBenefit>? reservedBenefits = null;
            if (request.AppliedBenefits is { Count: > 0 })
            {
                // Package credits are staged on this same unit of work: booking + reserve commit (or fail) together.
                if (benefits is null)
                {
                    return Result.Failure<BookingResponse>(Domain.Retention.RetentionErrorCodes.BenefitNotFoundError());
                }

                var reserved = await benefits.TryReserveBenefitsAsync(
                    new Application.Retention.ReserveBenefitsRequest(
                        request.TenantId,
                        customer.Id,
                        booking.Id,
                        businessLocationId,
                        orderedServices.Select(x => x.Id).ToArray(),
                        guestCount,
                        request.AppliedBenefits,
                        $"customer:{customer.Id}",
                        now),
                    cancellationToken);
                if (reserved.IsFailure)
                {
                    return Result.Failure<BookingResponse>(reserved.Error);
                }

                reservedBenefits = reserved.Value!;
            }

            if (request.LoyaltyPointsToRedeem > 0)
            {
                if (loyaltyRedeem is null)
                {
                    return Result.Failure<BookingResponse>(Domain.Retention.RetentionErrorCodes.LoyaltyRedeemInvalidError());
                }

                var cartTotal = booking.Lines.Sum(x => x.PriceAmount) * guestCount;
                var covered = CoveredAmountFromReserved(booking, reservedBenefits);
                var payable = Math.Max(0m, cartTotal - covered);
                var stagedRedeem = await loyaltyRedeem.StageRedeemForBookingAsync(
                    new Application.Retention.StageLoyaltyRedeemRequest(
                        request.TenantId,
                        customer.Id,
                        booking.Id,
                        request.LoyaltyPointsToRedeem,
                        payable,
                        $"customer:{customer.Id}",
                        now),
                    cancellationToken);
                if (stagedRedeem.IsFailure)
                {
                    dbContext.ChangeTracker.Clear();
                    domainEventCollector.TakeAll();
                    return Result.Failure<BookingResponse>(stagedRedeem.Error);
                }
            }

            dbContext.Bookings.Add(booking);
            try { await dbContext.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException ex) when (availabilityService.IsAdmissionConflict(ex))
            {
                // A synchronized busy snapshot changed after admission. Nothing in this save may commit.
                dbContext.ChangeTracker.Clear();
                domainEventCollector.TakeAll();
                return Result.Failure<BookingResponse>(ErrorCodes.SlotUnavailableError());
            }
            catch (DbUpdateException ex) when (benefits is not null && benefits.IsBenefitConflict(ex))
            {
                // Another booking took the last credit first; the booking row must not commit without its reserve.
                dbContext.ChangeTracker.Clear();
                domainEventCollector.TakeAll();
                return Result.Failure<BookingResponse>(Domain.Retention.RetentionErrorCodes.BenefitInsufficientError());
            }
            await TenantRealtimeNotifications.PublishPendingBookingsAsync(
                tenantRealtime,
                dbContext,
                request.TenantId,
                cancellationToken);

            if (booking.Status == BookingStatus.Confirmed)
            {
                await tenantCustomers.EnsureFromBookingAsync(request.TenantId, customer.Id, cancellationToken);
            }

            return Result.Success(
                BookingMapper.ToResponse(booking, primary.Name, staffDisplayName, primary.Currency));
        }
    }

    public async Task<PagedResult<BookingResponse>> ListForTenantAsync(
        Guid tenantId,
        string tab = TenantBookingTabs.Pending,
        int page = 1,
        int pageSize = Paging.DefaultPageSize,
        Guid? staffMemberId = null,
        Guid? locationId = null,
        string? sortBy = null,
        ListSortDirection? sortDirection = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Paging.Normalize(page, pageSize);
        if (normalized.IsFailure)
        {
            return new PagedResult<BookingResponse>(Array.Empty<BookingResponse>(), page, pageSize, 0);
        }

        (page, pageSize) = normalized.Value!;
        var effectiveTab = TenantBookingTabs.IsValid(tab) ? tab : TenantBookingTabs.Pending;
        var startOfTodayUtc = DateTimeOffset.UtcNow.Date;

        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        var multiLocationReporting = tenant is not null
            && entitlementsService.GetEntitlementsForTenant(tenant).MultiLocationReporting;
        var locationCheck = await TenantLocationScope.ResolveListLocationAsync(
            dbContext,
            tenantId,
            locationId,
            multiLocationReporting,
            cancellationToken);
        if (locationCheck.IsFailure)
        {
            return new PagedResult<BookingResponse>(Array.Empty<BookingResponse>(), page, pageSize, 0);
        }

        locationId = locationCheck.Value;

        var query = dbContext.Bookings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);

        if (staffMemberId is Guid scopedStaffId)
        {
            query = query.Where(x => x.StaffMemberId == scopedStaffId);
        }

        query = query.FilterBookingsByLocation(locationId);

        query = ApplyTenantBookingTabFilter(query, effectiveTab, startOfTodayUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        IOrderedQueryable<BookingRecord> ordered;
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            ordered = effectiveTab switch
            {
                TenantBookingTabs.Past => query.OrderByDescending(x => x.StartAt),
                _ => query.OrderBy(x => x.StartAt),
            };
        }
        else
        {
            var field = sortBy;
            var direction = sortDirection ?? TenantBookingListSort.DefaultDirection;
            ordered = ApplyTenantBookingSort(query, field, direction);
        }

        var bookingIds = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (bookingIds.Count == 0)
        {
            return new PagedResult<BookingResponse>(Array.Empty<BookingResponse>(), page, pageSize, totalCount);
        }

        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.Guests)
            .Where(x => bookingIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        bookings = bookingIds
            .Select(id => bookings.First(b => b.Id == id))
            .ToList();

        if (bookings.Count == 0)
        {
            return new PagedResult<BookingResponse>(Array.Empty<BookingResponse>(), page, pageSize, totalCount);
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
        var customerIds = bookings.Select(x => x.CustomerId).Distinct().ToArray();
        var customers = await dbContext.Customers.AsNoTracking()
            .Where(x => customerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var items = bookings
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

                customers.TryGetValue(booking.CustomerId, out var customer);
                var guestName = booking.Guests?
                    .OrderBy(x => x.SortOrder)
                    .Select(x => x.DisplayName)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                return BookingMapper.ToResponse(
                    booking,
                    name,
                    staffName,
                    service?.Currency,
                    customerName: guestName ?? customer?.Name,
                    customerPhoneMasked: PiiMasker.MaskPhone(customer?.Phone));
            })
            .ToArray();

        return new PagedResult<BookingResponse>(items, page, pageSize, totalCount);
    }

    private static IQueryable<BookingRecord> ApplyTenantBookingTabFilter(
        IQueryable<BookingRecord> query,
        string tab,
        DateTime startOfTodayUtc)
    {
        if (tab == TenantBookingTabs.All)
        {
            return query;
        }

        if (tab == TenantBookingTabs.Pending)
        {
            return query.Where(x => x.Status == BookingStatus.Pending);
        }

        if (tab == TenantBookingTabs.Upcoming)
        {
            return query.Where(x =>
                (x.Status == BookingStatus.Confirmed
                 || x.Status == BookingStatus.CheckedIn
                 || x.Status == BookingStatus.Completed)
                && x.StartAt >= startOfTodayUtc);
        }

        return query.Where(x =>
            x.Status == BookingStatus.Rejected
            || x.Status == BookingStatus.Cancelled
            || x.Status == BookingStatus.Paid
            || ((x.Status == BookingStatus.Confirmed
                 || x.Status == BookingStatus.CheckedIn
                 || x.Status == BookingStatus.Completed)
                && x.StartAt < startOfTodayUtc));
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
        var receiptTokenByBooking = await LoadReceiptPublicTokensByBookingAsync(bookingIds, cancellationToken);
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

                receiptTokenByBooking.TryGetValue(booking.Id, out var receiptToken);

                return BookingMapper.ToCustomerResponse(
                    booking,
                    name,
                    tenants.GetValueOrDefault(booking.TenantId, string.Empty),
                    slugByTenant.GetValueOrDefault(booking.TenantId, string.Empty),
                    canReview,
                    hasReview,
                    review?.Rating,
                    staffName,
                    service?.Currency,
                    receiptToken);
            })
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadReceiptPublicTokensByBookingAsync(
        Guid[] bookingIds,
        CancellationToken cancellationToken)
    {
        if (bookingIds.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await dbContext.BillingDocuments
            .AsNoTracking()
            .Where(d =>
                d.BookingId != null
                && bookingIds.Contains(d.BookingId.Value)
                && d.Status == BillingDocumentStatus.Issued
                && d.Kind == BillingDocumentKind.Receipt)
            .OrderByDescending(d => d.IssuedAt ?? d.CreatedAt)
            .Select(d => new { BookingId = d.BookingId!.Value, d.PublicToken })
            .ToListAsync(cancellationToken);

        var map = new Dictionary<Guid, string>();
        foreach (var row in rows)
        {
            map.TryAdd(row.BookingId, row.PublicToken);
        }

        return map;
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

    public Task<Result<BookingResponse>> CheckInAsync(
        Guid tenantId,
        Guid bookingId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        TransitionFloorAsync(
            tenantId,
            bookingId,
            BookingStatus.CheckedIn,
            requireStaffMemberId,
            markPaidRequest: null,
            cancellationToken);

    public Task<Result<BookingResponse>> CompleteAsync(
        Guid tenantId,
        Guid bookingId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        TransitionFloorAsync(
            tenantId,
            bookingId,
            BookingStatus.Completed,
            requireStaffMemberId,
            markPaidRequest: null,
            cancellationToken);

    public Task<Result<BookingResponse>> MarkPaidAsync(
        Guid tenantId,
        Guid bookingId,
        MarkBookingPaidRequest request,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default) =>
        TransitionFloorAsync(
            tenantId,
            bookingId,
            BookingStatus.Paid,
            requireStaffMemberId,
            request,
            cancellationToken);

    public async Task<Result<CollectBalanceResponse>> CollectBalanceAsync(
        Guid tenantId,
        Guid bookingId,
        Guid? requireStaffMemberId = null,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadTenantBookingAsync(tenantId, bookingId, requireStaffMemberId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<CollectBalanceResponse>(loaded.Error);
        }

        var (booking, service, staffName) = loaded.Value!;
        if (booking.Status is not (BookingStatus.Completed or BookingStatus.CheckedIn))
        {
            return Result.Failure<CollectBalanceResponse>(
                Error.Conflict("Collect balance is only available after check-in or completion."));
        }

        var remaining = await CalculateRemainingBalanceAsync(booking, cancellationToken);
        if (remaining <= 0)
        {
            return Result.Failure<CollectBalanceResponse>(
                Error.Conflict("No remaining balance to collect."));
        }

        var currency = service?.Currency ?? "NGN";
        var paymentResult = await paymentOrchestrator.InitializeAsync(
            new InitializePaymentRequest(
                tenantId,
                booking.Id,
                remaining,
                currency,
                "link",
                $"Balance — {booking.Lines.OrderBy(x => x.SortOrder).FirstOrDefault()?.ServiceName ?? service?.Name ?? "Service"}",
                CustomerEmail: null,
                CallbackUrl: null),
            idempotencyKey: null,
            cancellationToken);

        if (paymentResult.IsFailure)
        {
            return Result.Failure<CollectBalanceResponse>(paymentResult.Error);
        }

        var payment = paymentResult.Value!;
        var mapped = BookingMapper.ToResponse(
            booking,
            booking.Lines.OrderBy(x => x.SortOrder).FirstOrDefault()?.ServiceName ?? service?.Name ?? "Service",
            staffName,
            currency,
            remaining);

        return Result.Success(new CollectBalanceResponse(
            booking.Id,
            payment.Id,
            payment.Amount,
            payment.Currency,
            payment.CheckoutUrl,
            payment.ProviderReference,
            mapped));
    }

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
        domainEventCollector.Add(new SlotOpened(
            booking.TenantId,
            booking.ServiceOfferingId,
            booking.StartAt,
            DateTimeOffset.UtcNow));
        var releaseFailure = await StageBenefitReleaseAsync(booking, $"customer:{booking.CustomerId}", "booking_cancelled", cancellationToken);
        if (releaseFailure is not null)
        {
            dbContext.ChangeTracker.Clear();
            domainEventCollector.TakeAll();
            return Result.Failure<CustomerBookingResponse>(releaseFailure);
        }

        if (loyaltyRedeem is not null)
        {
            var releasedLoyalty = await loyaltyRedeem.ReleaseRedeemForBookingAsync(
                booking.TenantId, booking.Id, $"customer:{booking.CustomerId}", cancellationToken);
            if (releasedLoyalty.IsFailure)
            {
                dbContext.ChangeTracker.Clear();
                domainEventCollector.TakeAll();
                return Result.Failure<CustomerBookingResponse>(releasedLoyalty.Error);
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (benefits is not null && benefits.IsBenefitConflict(ex))
        {
            dbContext.ChangeTracker.Clear();
            domainEventCollector.TakeAll();
            return Result.Failure<CustomerBookingResponse>(Domain.Retention.RetentionErrorCodes.BenefitConflictError());
        }

        await TenantRealtimeNotifications.PublishPendingBookingsAsync(
            tenantRealtime,
            dbContext,
            booking.TenantId,
            cancellationToken);

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

    private async Task<Result<BookingResponse>> TransitionFloorAsync(
        Guid tenantId,
        Guid bookingId,
        BookingStatus target,
        Guid? requireStaffMemberId,
        MarkBookingPaidRequest? markPaidRequest,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadTenantBookingAsync(tenantId, bookingId, requireStaffMemberId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<BookingResponse>(loaded.Error);
        }

        var (booking, service, staffName) = loaded.Value!;
        var remaining = await CalculateRemainingBalanceAsync(booking, cancellationToken);

        switch (target)
        {
            case BookingStatus.CheckedIn when booking.Status != BookingStatus.Confirmed:
                return Result.Failure<BookingResponse>(
                    Error.Conflict("Only confirmed bookings can be checked in."));
            case BookingStatus.Completed when booking.Status != BookingStatus.CheckedIn:
                return Result.Failure<BookingResponse>(
                    Error.Conflict("Only checked-in bookings can be completed."));
            case BookingStatus.Paid when booking.Status != BookingStatus.Completed:
                return Result.Failure<BookingResponse>(
                    Error.Conflict("Only completed bookings can be marked paid."));
            case BookingStatus.CheckedIn:
            case BookingStatus.Completed:
            case BookingStatus.Paid:
                break;
            default:
                return Result.Failure<BookingResponse>(Error.Conflict("Unsupported floor transition."));
        }

        var now = DateTimeOffset.UtcNow;
        booking.Status = target;
        booking.UpdatedAt = now;

        if (target == BookingStatus.Completed && remaining <= 0)
        {
            booking.Status = BookingStatus.Paid;
            booking.SettlementSource = await CoveredAmountAsync(booking, cancellationToken) > 0
                && !await dbContext.PaymentIntents.AsNoTracking().AnyAsync(
                    x => x.BookingId == booking.Id && x.TenantId == booking.TenantId && x.Status == PaymentIntentStatus.Completed,
                    cancellationToken)
                    ? BookingSettlementSource.PackageCredit
                    : BookingSettlementSource.Online;
            booking.PaidAt = now;
        }
        else if (target == BookingStatus.Paid)
        {
            booking.SettlementSource = BookingSettlementSource.ExternalTill;
            booking.PaidAt = now;
            booking.ExternalPaymentReference = string.IsNullOrWhiteSpace(markPaidRequest?.ExternalReference)
                ? null
                : markPaidRequest.ExternalReference.Trim();
            if (!string.IsNullOrWhiteSpace(markPaidRequest?.Note))
            {
                var note = markPaidRequest.Note.Trim();
                booking.BusinessNotes = string.IsNullOrWhiteSpace(booking.BusinessNotes)
                    ? note
                    : $"{booking.BusinessNotes}\n{note}";
            }
        }

        if (target == BookingStatus.Completed && benefits is not null)
        {
            // Reserved credits become used in the same SaveChanges as the status change.
            var finalized = await benefits.FinalizeRedemptionsAsync(
                tenantId, booking.Id, currentUser.UserId ?? "portal:unknown", cancellationToken);
            if (finalized.IsFailure)
            {
                dbContext.ChangeTracker.Clear();
                domainEventCollector.TakeAll();
                return Result.Failure<BookingResponse>(finalized.Error);
            }
        }

        if (booking.Status == BookingStatus.Paid && loyaltyEarn is not null)
        {
            var staged = await loyaltyEarn.StageEarnForPaidBookingAsync(
                tenantId, booking.Id, currentUser.UserId ?? "portal:unknown", cancellationToken);
            if (staged.IsFailure)
            {
                dbContext.ChangeTracker.Clear();
                domainEventCollector.TakeAll();
                return Result.Failure<BookingResponse>(staged.Error);
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (benefits is not null && benefits.IsBenefitConflict(ex))
        {
            dbContext.ChangeTracker.Clear();
            domainEventCollector.TakeAll();
            return Result.Failure<BookingResponse>(Domain.Retention.RetentionErrorCodes.BenefitConflictError());
        }

        await WriteBookingStatusAuditAsync(tenantId, bookingId, booking.Status, cancellationToken);

        if (booking.Status == BookingStatus.Paid
            && booking.SettlementSource == BookingSettlementSource.ExternalTill)
        {
            await billingDocuments.IssueReceiptForTillPaymentAsync(
                tenantId,
                booking.Id,
                booking.ExternalPaymentReference,
                cancellationToken);
        }

        if (booking.Status == BookingStatus.Paid)
        {
            var currency = service?.Currency ?? "NGN";
            if (markPaidRequest?.TipAmount is > 0)
            {
                await staffCompensation.RecordTipForTillPaymentAsync(
                    tenantId,
                    booking.Id,
                    markPaidRequest.TipAmount.Value,
                    markPaidRequest.TipStaffMemberId,
                    currency,
                    cancellationToken);
            }

            await staffCompensation.AccrueCommissionForPaidBookingAsync(tenantId, booking.Id, cancellationToken);
        }

        remaining = await CalculateRemainingBalanceAsync(booking, cancellationToken);
        var name = booking.Lines.OrderBy(x => x.SortOrder).FirstOrDefault()?.ServiceName
            ?? service?.Name
            ?? "Service";
        return Result.Success(BookingMapper.ToResponse(
            booking,
            name,
            staffName,
            service?.Currency,
            booking.Status == BookingStatus.Paid ? 0m : remaining));
    }

    private async Task<Result<(BookingRecord Booking, ServiceOffering? Service, string? StaffName)>> LoadTenantBookingAsync(
        Guid tenantId,
        Guid bookingId,
        Guid? requireStaffMemberId,
        CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings
            .Include(x => x.Lines)
            .Include(x => x.Guests)
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.TenantId == tenantId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure<(BookingRecord, ServiceOffering?, string?)>(Error.NotFound("Booking"));
        }

        if (requireStaffMemberId is Guid requiredStaff
            && booking.StaffMemberId != requiredStaff)
        {
            return Result.Failure<(BookingRecord, ServiceOffering?, string?)>(ErrorCodes.PermissionDeniedError());
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

        return Result.Success((booking, service, staffName));
    }

    private static decimal CoveredAmountFromReserved(
        BookingRecord booking,
        IReadOnlyList<Application.Retention.ReservedBenefit>? reserved)
    {
        if (reserved is null or { Count: 0 })
        {
            return 0m;
        }

        var amount = 0m;
        foreach (var item in reserved)
        {
            amount += booking.Lines
                .Where(x => x.ServiceOfferingId == item.ServiceOfferingId)
                .OrderBy(x => x.SortOrder)
                .Take(item.Quantity)
                .Sum(x => x.PriceAmount);
        }

        return amount;
    }

    /// <summary>Price of booking lines already paid for by package credits (reserved or redeemed).</summary>
    private async Task<decimal> CoveredAmountAsync(BookingRecord booking, CancellationToken cancellationToken)
    {
        if (benefits is null)
        {
            return 0m;
        }

        var covered = await benefits.GetCoveredServicesAsync(booking.TenantId, booking.Id, cancellationToken);
        var amount = 0m;
        foreach (var item in covered)
        {
            amount += booking.Lines
                .Where(x => x.ServiceOfferingId == item.ServiceOfferingId)
                .OrderBy(x => x.SortOrder)
                .Take(item.Quantity)
                .Sum(x => x.PriceAmount);
        }

        return amount;
    }

    private async Task<Error?> StageBenefitReleaseAsync(
        BookingRecord booking, string actor, string reason, CancellationToken cancellationToken)
    {
        if (benefits is null)
        {
            return null;
        }

        var released = await benefits.ReleaseReservationsAsync(booking.TenantId, booking.Id, actor, reason, cancellationToken);
        return released.IsFailure ? released.Error : null;
    }

    private async Task<decimal> CalculateRemainingBalanceAsync(
        BookingRecord booking,
        CancellationToken cancellationToken)
    {
        var total = booking.Lines.Sum(x => x.PriceAmount);
        if (total <= 0 && booking.ServiceOfferingId != Guid.Empty)
        {
            var servicePrice = await dbContext.ServiceOfferings
                .AsNoTracking()
                .Where(x => x.Id == booking.ServiceOfferingId)
                .Select(x => (decimal?)x.PriceAmount)
                .FirstOrDefaultAsync(cancellationToken);
            total = servicePrice ?? 0m;
        }

        total = Math.Max(0m, total - await CoveredAmountAsync(booking, cancellationToken));
        if (loyaltyRedeem is not null)
        {
            total = Math.Max(0m, total - await loyaltyRedeem.GetDiscountAmountForBookingAsync(booking.TenantId, booking.Id, cancellationToken));
        }

        var paid = await dbContext.PaymentIntents
            .AsNoTracking()
            .Where(x =>
                x.BookingId == booking.Id
                && x.TenantId == booking.TenantId
                && x.Status == PaymentIntentStatus.Completed)
            .SumAsync(x => x.Amount, cancellationToken);

        return Math.Max(0m, total - paid);
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

        if (status == BookingStatus.Confirmed)
        {
            await tenantCustomers.EnsureFromBookingAsync(tenantId, booking.CustomerId, cancellationToken);
        }

        if (status == BookingStatus.Rejected)
        {
            var releaseFailure = await StageBenefitReleaseAsync(
                booking, currentUser.UserId ?? "portal:unknown", "booking_rejected", cancellationToken);
            if (releaseFailure is not null)
            {
                dbContext.ChangeTracker.Clear();
                domainEventCollector.TakeAll();
                return Result.Failure<BookingResponse>(releaseFailure);
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (benefits is not null && benefits.IsBenefitConflict(ex))
        {
            dbContext.ChangeTracker.Clear();
            domainEventCollector.TakeAll();
            return Result.Failure<BookingResponse>(Domain.Retention.RetentionErrorCodes.BenefitConflictError());
        }

        await WriteBookingStatusAuditAsync(tenantId, bookingId, status, cancellationToken);
        await TenantRealtimeNotifications.PublishPendingBookingsAsync(
            tenantRealtime,
            dbContext,
            tenantId,
            cancellationToken);
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

    private async Task WriteBookingStatusAuditAsync(
        Guid tenantId,
        Guid bookingId,
        BookingStatus status,
        CancellationToken cancellationToken)
    {
        var action = status switch
        {
            BookingStatus.Confirmed => AuditActions.BookingAccepted,
            BookingStatus.Rejected => AuditActions.BookingRejected,
            BookingStatus.CheckedIn => AuditActions.BookingCheckedIn,
            BookingStatus.Completed => AuditActions.BookingCompleted,
            BookingStatus.Paid => AuditActions.BookingMarkedPaid,
            _ => null,
        };

        if (action is null)
        {
            return;
        }

        var actorId = currentUser.UserId ?? "portal:unknown";
        await auditLogWriter.WriteAsync(
            new AuditEntry(
                Guid.NewGuid(),
                actorId,
                action,
                "booking",
                bookingId.ToString(),
                correlationContext.CorrelationId,
                DateTimeOffset.UtcNow,
                $"{{\"tenantId\":\"{tenantId}\",\"status\":\"{status.ToString().ToLowerInvariant()}\"}}"),
            cancellationToken);
    }

    private IOrderedQueryable<BookingRecord> ApplyTenantBookingSort(
        IQueryable<BookingRecord> query,
        string field,
        ListSortDirection direction)
    {
        var desc = ListSort.IsDescending(direction);
        return field switch
        {
            "customer" => desc
                ? query.OrderByDescending(x =>
                    dbContext.Customers.Where(c => c.Id == x.CustomerId).Select(c => c.Name).FirstOrDefault()
                    ?? string.Empty)
                : query.OrderBy(x =>
                    dbContext.Customers.Where(c => c.Id == x.CustomerId).Select(c => c.Name).FirstOrDefault()
                    ?? string.Empty),
            "service" => desc
                ? query.OrderByDescending(x =>
                    dbContext.ServiceOfferings.Where(s => s.Id == x.ServiceOfferingId).Select(s => s.Name).FirstOrDefault()
                    ?? string.Empty)
                : query.OrderBy(x =>
                    dbContext.ServiceOfferings.Where(s => s.Id == x.ServiceOfferingId).Select(s => s.Name).FirstOrDefault()
                    ?? string.Empty),
            "status" => desc
                ? query.OrderByDescending(x => x.Status)
                : query.OrderBy(x => x.Status),
            _ => desc
                ? query.OrderByDescending(x => x.StartAt)
                : query.OrderBy(x => x.StartAt),
        };
    }
}
