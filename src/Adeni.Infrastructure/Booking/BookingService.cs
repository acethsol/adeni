namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Caching;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class BookingService(
    AdeniDbContext dbContext,
    IAvailabilityService availabilityService,
    IDistributedLockProvider lockProvider) : IBookingService
{
    public async Task<Result<BookingResponse>> CreateAsync(
        string customerAuth0Sub,
        CreateBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<BookingResponse>(Error.Forbidden("Customer authentication is required."));
        }

        if (request.StartAt <= DateTimeOffset.UtcNow)
        {
            return Result.Failure<BookingResponse>(Error.Validation("Booking start must be in the future."));
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == request.TenantId && t.Status == TenantStatus.Verified,
                cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<BookingResponse>(Error.NotFound("Business"));
        }

        var service = await dbContext.ServiceOfferings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == request.ServiceOfferingId
                    && x.TenantId == request.TenantId
                    && x.IsActive,
                cancellationToken);

        if (service is null)
        {
            return Result.Failure<BookingResponse>(Error.NotFound("Service"));
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

        var lockKey = CacheKeys.SlotLock(request.TenantId, request.StartAt, service.Id);
        var slotLock = await lockProvider.TryAcquireAsync(lockKey, CacheTtl.SlotLock, cancellationToken);
        if (slotLock is null)
        {
            return Result.Failure<BookingResponse>(Error.Conflict("That time slot is being booked. Try again."));
        }

        await using (slotLock)
        {
            var isAvailable = await availabilityService.IsSlotAvailableAsync(
                request.TenantId,
                service.Id,
                request.StartAt,
                service.DurationMinutes,
                cancellationToken);

            if (!isAvailable)
            {
                return Result.Failure<BookingResponse>(Error.Conflict("That time slot is no longer available."));
            }

            var now = DateTimeOffset.UtcNow;
            var booking = new BookingRecord
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                ServiceOfferingId = service.Id,
                CustomerId = customer.Id,
                StartAt = request.StartAt,
                EndAt = request.StartAt.AddMinutes(service.DurationMinutes),
                Status = BookingStatus.Pending,
                CustomerNotes = string.IsNullOrWhiteSpace(request.CustomerNotes)
                    ? null
                    : request.CustomerNotes.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            dbContext.Bookings.Add(booking);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Result.Success(BookingMapper.ToResponse(booking, service.Name));
        }
    }

    public async Task<IReadOnlyList<BookingResponse>> ListForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Join(
                dbContext.ServiceOfferings.AsNoTracking(),
                booking => booking.ServiceOfferingId,
                service => service.Id,
                (booking, service) => new { booking, service.Name })
            .OrderBy(x => x.booking.StartAt)
            .ToListAsync(cancellationToken);

        return bookings
            .Select(x => BookingMapper.ToResponse(x.booking, x.Name))
            .ToArray();
    }

    public Task<Result<BookingResponse>> AcceptAsync(
        Guid tenantId,
        Guid bookingId,
        CancellationToken cancellationToken = default) =>
        UpdateStatusAsync(tenantId, bookingId, BookingStatus.Confirmed, null, cancellationToken);

    public Task<Result<BookingResponse>> RejectAsync(
        Guid tenantId,
        Guid bookingId,
        string? reason,
        CancellationToken cancellationToken = default) =>
        UpdateStatusAsync(tenantId, bookingId, BookingStatus.Rejected, reason, cancellationToken);

    private async Task<Result<BookingResponse>> UpdateStatusAsync(
        Guid tenantId,
        Guid bookingId,
        BookingStatus status,
        string? businessNotes,
        CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.TenantId == tenantId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure<BookingResponse>(Error.NotFound("Booking"));
        }

        if (booking.Status != BookingStatus.Pending)
        {
            return Result.Failure<BookingResponse>(Error.Conflict("Only pending bookings can be updated."));
        }

        booking.Status = status;
        booking.BusinessNotes = string.IsNullOrWhiteSpace(businessNotes) ? null : businessNotes.Trim();
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        var serviceName = await dbContext.ServiceOfferings
            .AsNoTracking()
            .Where(x => x.Id == booking.ServiceOfferingId)
            .Select(x => x.Name)
            .FirstAsync(cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(BookingMapper.ToResponse(booking, serviceName));
    }
}
