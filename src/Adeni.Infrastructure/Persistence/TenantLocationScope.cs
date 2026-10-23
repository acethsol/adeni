namespace Adeni.Infrastructure.Persistence;

using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Messaging;
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
            ? query.Where(x => x.BusinessLocationId == id)
            : query;

    /// <summary>
    /// Resolves optional branch filter for tenant list APIs: validates id, defaults to primary when
    /// 2+ active locations and cross-branch reporting is not entitled, otherwise null = all branches.
    /// </summary>
    public static async Task<Result<Guid?>> ResolveListLocationAsync(
        AdeniDbContext dbContext,
        Guid tenantId,
        Guid? locationId,
        bool multiLocationReporting,
        CancellationToken cancellationToken = default)
    {
        if (locationId is Guid explicitId)
        {
            return await ValidateOptionalLocationIdAsync(
                dbContext,
                tenantId,
                explicitId,
                cancellationToken);
        }

        var activeCount = await dbContext.BusinessLocations.AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);

        if (activeCount <= 1)
        {
            if (activeCount == 0)
            {
                return Result.Success<Guid?>(null);
            }

            var onlyId = await dbContext.BusinessLocations.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.IsActive)
                .Select(x => (Guid?)x.Id)
                .FirstAsync(cancellationToken);
            return Result.Success(onlyId);
        }

        if (multiLocationReporting)
        {
            return Result.Success<Guid?>(null);
        }

        var primary = await ResolveAvailabilityLocationAsync(
            dbContext,
            tenantId,
            null,
            cancellationToken);
        return primary.IsFailure
            ? Result.Failure<Guid?>(primary.Error!)
            : Result.Success<Guid?>(primary.Value);
    }

    public static IQueryable<Conversation> FilterConversationsByLocation(
        this IQueryable<Conversation> query,
        AdeniDbContext dbContext,
        Guid tenantId,
        Guid? locationId)
    {
        if (locationId is not Guid branchId)
        {
            return query;
        }

        return query.Where(c =>
            c.ContextType != ConversationContextType.Booking
            || c.ContextId == null
            || dbContext.Bookings.Any(b =>
                b.TenantId == tenantId
                && b.Id == c.ContextId
                && b.BusinessLocationId == branchId));
    }

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
