namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

public sealed class AvailabilityService(
    AdeniDbContext dbContext,
    ITenantSchedulingTimeZone tenantSchedulingTimeZone) : IAvailabilityService
{
    public async Task<IReadOnlyList<WeeklyAvailabilityRule>> GetWeeklyRulesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var rules = await dbContext.WeeklyAvailabilities
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.OpenTime)
            .ToListAsync(cancellationToken);

        return rules
            .Select(x => new WeeklyAvailabilityRule(x.DayOfWeek, x.OpenTime, x.CloseTime))
            .ToArray();
    }

    public async Task<Result<IReadOnlyList<WeeklyAvailabilityRule>>> ReplaceWeeklyRulesAsync(
        Guid tenantId,
        IReadOnlyList<WeeklyAvailabilityRule> rules,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateRules(rules);
        if (validation.IsFailure)
        {
            return Result.Failure<IReadOnlyList<WeeklyAvailabilityRule>>(validation.Error);
        }

        if (!await dbContext.Tenants.AsNoTracking().AnyAsync(t => t.Id == tenantId, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<WeeklyAvailabilityRule>>(Error.NotFound("Tenant"));
        }

        var existing = await dbContext.WeeklyAvailabilities
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        dbContext.WeeklyAvailabilities.RemoveRange(existing);

        foreach (var rule in rules)
        {
            dbContext.WeeklyAvailabilities.Add(new WeeklyAvailability
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                DayOfWeek = rule.DayOfWeek,
                OpenTime = rule.OpenTime,
                CloseTime = rule.CloseTime
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success(rules);
    }

    public Task<Result<IReadOnlyList<AvailableSlotResponse>>> GetAvailableSlotsAsync(
        Guid tenantId,
        Guid serviceId,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken cancellationToken = default) =>
        GetAvailableSlotsInternalAsync(tenantId, null, serviceId, rangeStart, rangeEnd, cancellationToken);

    public async Task<Result<IReadOnlyList<AvailableSlotResponse>>> GetAvailableSlotsBySlugAsync(
        string slug,
        Guid serviceId,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<IReadOnlyList<AvailableSlotResponse>>(Error.Validation("Business slug is required."));
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var match = await VerifiedLocationQueries.ResolveLocationBySlugAsync(
            dbContext,
            normalizedSlug,
            cancellationToken);

        if (match is null)
        {
            return Result.Failure<IReadOnlyList<AvailableSlotResponse>>(Error.NotFound("Business"));
        }

        return await GetAvailableSlotsInternalAsync(
            match.Value.TenantId,
            match.Value.LocationId,
            serviceId,
            rangeStart,
            rangeEnd,
            cancellationToken);
    }

    public async Task<bool> IsSlotAvailableAsync(
        Guid tenantId,
        Guid serviceId,
        DateTimeOffset startAt,
        int durationMinutes,
        CancellationToken cancellationToken = default)
    {
        var service = await dbContext.ServiceOfferings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == serviceId && x.TenantId == tenantId && x.IsActive,
                cancellationToken);

        if (service is null)
        {
            return false;
        }

        var rules = await GetWeeklyRulesAsync(tenantId, cancellationToken);
        var schedulingTimeZone = await tenantSchedulingTimeZone.ForTenantAsync(tenantId, cancellationToken);
        if (!SlotGenerator.FitsWeeklyRules(schedulingTimeZone, rules, startAt, durationMinutes))
        {
            return false;
        }

        var endAt = startAt.AddMinutes(durationMinutes);
        var existing = await dbContext.Bookings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return !BookingConflictChecker.OverlapsExisting(existing, startAt, endAt);
    }

    private async Task<Result<IReadOnlyList<AvailableSlotResponse>>> GetAvailableSlotsInternalAsync(
        Guid tenantId,
        Guid? locationId,
        Guid serviceId,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken cancellationToken)
    {
        if (rangeEnd <= rangeStart)
        {
            return Result.Failure<IReadOnlyList<AvailableSlotResponse>>(
                Error.Validation("Range end must be after range start."));
        }

        if ((rangeEnd - rangeStart).TotalDays > 14)
        {
            return Result.Failure<IReadOnlyList<AvailableSlotResponse>>(
                Error.Validation("Slot range cannot exceed 14 days."));
        }

        var service = await dbContext.ServiceOfferings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == serviceId && x.TenantId == tenantId && x.IsActive,
                cancellationToken);

        if (service is null)
        {
            return Result.Failure<IReadOnlyList<AvailableSlotResponse>>(Error.NotFound("Service"));
        }

        var rules = await GetWeeklyRulesAsync(tenantId, cancellationToken);
        var schedulingTimeZone = locationId is Guid resolvedLocationId
            ? await tenantSchedulingTimeZone.ForLocationAsync(resolvedLocationId, cancellationToken)
            : await tenantSchedulingTimeZone.ForTenantAsync(tenantId, cancellationToken);
        var existing = await dbContext.Bookings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var slots = SlotGenerator
            .GenerateSlotStarts(schedulingTimeZone, rules, rangeStart, rangeEnd, service.DurationMinutes)
            .Where(start =>
            {
                var end = start.AddMinutes(service.DurationMinutes);
                return !BookingConflictChecker.OverlapsExisting(existing, start, end);
            })
            .Select(start => new AvailableSlotResponse(
                start,
                start.AddMinutes(service.DurationMinutes)))
            .ToArray();

        return Result.Success<IReadOnlyList<AvailableSlotResponse>>(slots);
    }

    private static Result ValidateRules(IReadOnlyList<WeeklyAvailabilityRule> rules)
    {
        foreach (var rule in rules)
        {
            if (rule.CloseTime <= rule.OpenTime)
            {
                return Result.Failure(Error.Validation("Close time must be after open time."));
            }
        }

        return Result.Success();
    }
}
