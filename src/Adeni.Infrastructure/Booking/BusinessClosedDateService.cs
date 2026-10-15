namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class BusinessClosedDateService(AdeniDbContext dbContext) : IBusinessClosedDateService
{
    private const int MaxRangeDays = 366;
    private const int MaxActiveRanges = 100;

    public async Task<Result<IReadOnlyList<BusinessClosedDateResponse>>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var items = await dbContext.BusinessClosedDates
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.StartDate)
            .ThenBy(x => x.EndDate)
            .Select(x => new BusinessClosedDateResponse(
                x.Id,
                x.StartDate,
                x.EndDate,
                x.Reason,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<BusinessClosedDateResponse>>(items);
    }

    public async Task<Result<BusinessClosedDateResponse>> CreateAsync(
        Guid tenantId,
        CreateBusinessClosedDateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            return Result.Failure<BusinessClosedDateResponse>(Error.NotFound("Tenant"));
        }

        if (request.EndDate < request.StartDate)
        {
            return Result.Failure<BusinessClosedDateResponse>(
                Error.Validation("End date must be on or after start date."));
        }

        var spanDays = request.EndDate.DayNumber - request.StartDate.DayNumber + 1;
        if (spanDays > MaxRangeDays)
        {
            return Result.Failure<BusinessClosedDateResponse>(
                Error.Validation($"A closed range cannot exceed {MaxRangeDays} days."));
        }

        var activeCount = await dbContext.BusinessClosedDates
            .CountAsync(x => x.TenantId == tenantId, cancellationToken);
        if (activeCount >= MaxActiveRanges)
        {
            return Result.Failure<BusinessClosedDateResponse>(
                Error.Validation($"A business can have at most {MaxActiveRanges} closed ranges."));
        }

        var overlaps = await dbContext.BusinessClosedDates
            .AsNoTracking()
            .AnyAsync(
                x => x.TenantId == tenantId
                    && x.StartDate <= request.EndDate
                    && x.EndDate >= request.StartDate,
                cancellationToken);

        if (overlaps)
        {
            return Result.Failure<BusinessClosedDateResponse>(
                Error.Conflict("That range overlaps an existing closed date range."));
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? null
            : request.Reason.Trim();
        if (reason is { Length: > 200 })
        {
            reason = reason[..200];
        }

        var entity = new BusinessClosedDate
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Reason = reason,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        dbContext.BusinessClosedDates.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var conflicting = await dbContext.Bookings
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && (x.Status == BookingStatus.Pending || x.Status == BookingStatus.Confirmed)
                && x.StartAt < request.EndDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)
                && x.EndAt > request.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            .Select(x => x.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        // Refine conflict list by local calendar date overlap is approximate above;
        // return ids that fall on closed calendar days via UTC date bounds (good enough warn).
        return Result.Success(new BusinessClosedDateResponse(
            entity.Id,
            entity.StartDate,
            entity.EndDate,
            entity.Reason,
            entity.CreatedAt,
            conflicting.Count == 0 ? null : conflicting));
    }

    public async Task<Result> DeleteAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.BusinessClosedDates
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure(Error.NotFound("Closed date range"));
        }

        dbContext.BusinessClosedDates.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public Task<bool> IsClosedAsync(
        Guid tenantId,
        DateOnly localDate,
        CancellationToken cancellationToken = default) =>
        dbContext.BusinessClosedDates
            .AsNoTracking()
            .AnyAsync(
                x => x.TenantId == tenantId
                    && x.StartDate <= localDate
                    && x.EndDate >= localDate,
                cancellationToken);

    public async Task<IReadOnlyList<(DateOnly StartDate, DateOnly EndDate)>> ListRangesAsync(
        Guid tenantId,
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.BusinessClosedDates
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.StartDate <= toInclusive
                && x.EndDate >= fromInclusive)
            .Select(x => new { x.StartDate, x.EndDate })
            .ToListAsync(cancellationToken);

        return rows.Select(x => (x.StartDate, x.EndDate)).ToArray();
    }
}
