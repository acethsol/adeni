namespace Adeni.Infrastructure.Persistence;

using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Microsoft.EntityFrameworkCore;

public static class TenantLocationScope
{
    public static IQueryable<BookingRecord> FilterBookingsByLocation(
        this IQueryable<BookingRecord> query,
        Guid? locationId) =>
        locationId is Guid id
            ? query.Where(x => x.BusinessLocationId == id)
            : query;

    public static IQueryable<QueueTicket> FilterQueueByLocation(
        this IQueryable<QueueTicket> query,
        Guid? locationId) =>
        locationId is Guid id
            ? query.Where(x => x.BusinessLocationId == id)
            : query;

    public static IQueryable<StaffMember> FilterStaffByLocation(
        this IQueryable<StaffMember> query,
        Guid? locationId) =>
        locationId is Guid id
            ? query.Where(x => x.BusinessLocationId == null || x.BusinessLocationId == id)
            : query;

    public static async Task<Result<Guid?>> ValidateOptionalLocationIdAsync(
        AdeniDbContext dbContext,
        Guid tenantId,
        Guid? locationId,
        CancellationToken cancellationToken = default)
    {
        if (locationId is not Guid id)
        {
            return Result.Success<Guid?>(null);
        }

        var exists = await dbContext.BusinessLocations.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Id == id && x.IsActive, cancellationToken);

        return exists
            ? Result.Success<Guid?>(id)
            : Result.Failure<Guid?>(Error.Validation("Unknown or inactive location for this business."));
    }

    public static async Task<Result<Guid>> ResolveAvailabilityLocationAsync(
        AdeniDbContext dbContext,
        Guid tenantId,
        Guid? locationId,
        CancellationToken cancellationToken = default)
    {
        if (locationId is Guid id)
        {
            var validated = await ValidateOptionalLocationIdAsync(
                dbContext,
                tenantId,
                id,
                cancellationToken);
            if (validated.IsFailure)
            {
                return Result.Failure<Guid>(validated.Error!);
            }

            return Result.Success(id);
        }

        var defaultId = await dbContext.BusinessLocations.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.CreatedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return defaultId is Guid resolved
            ? Result.Success(resolved)
            : Result.Failure<Guid>(Error.NotFound("Business location"));
    }
}
