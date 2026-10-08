namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
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
        Guid? staffMemberId = null,
        IReadOnlyList<Guid>? additionalServiceIds = null,
        int guestCount = 1,
        CancellationToken cancellationToken = default) =>
        GetAvailableSlotsInternalAsync(
            tenantId,
            null,
            serviceId,
            rangeStart,
            rangeEnd,
            staffMemberId,
            additionalServiceIds,
            guestCount,
            cancellationToken);

    public async Task<Result<IReadOnlyList<AvailableSlotResponse>>> GetAvailableSlotsBySlugAsync(
        string slug,
        Guid serviceId,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        Guid? staffMemberId = null,
        IReadOnlyList<Guid>? additionalServiceIds = null,
        int guestCount = 1,
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
            staffMemberId,
            additionalServiceIds,
            guestCount,
            cancellationToken);
    }

    public async Task<bool> IsSlotAvailableAsync(
        Guid tenantId,
        Guid serviceId,
        DateTimeOffset startAt,
        int durationMinutes,
        Guid? staffMemberId = null,
        IReadOnlyList<Guid>? additionalServiceIds = null,
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

        var eligibleStaffIds = await GetEligibleStaffIdsAsync(tenantId, serviceId, cancellationToken);
        if (additionalServiceIds is { Count: > 0 })
        {
            foreach (var extraId in additionalServiceIds.Where(id => id != serviceId))
            {
                var extraEligible = await GetEligibleStaffIdsAsync(tenantId, extraId, cancellationToken);
                if (eligibleStaffIds.Count == 0)
                {
                    eligibleStaffIds = extraEligible;
                }
                else if (extraEligible.Count > 0)
                {
                    eligibleStaffIds = eligibleStaffIds.Intersect(extraEligible).ToArray();
                }
            }
        }

        if (staffMemberId is Guid requestedStaffId
            && eligibleStaffIds.Count > 0
            && !eligibleStaffIds.Contains(requestedStaffId))
        {
            return false;
        }

        return BookingConflictChecker.IsStaffSlotAvailable(
            existing,
            eligibleStaffIds,
            startAt,
            endAt,
            staffMemberId);
    }

    private async Task<Result<IReadOnlyList<AvailableSlotResponse>>> GetAvailableSlotsInternalAsync(
        Guid tenantId,
        Guid? locationId,
        Guid serviceId,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        Guid? staffMemberId,
        IReadOnlyList<Guid>? additionalServiceIds,
        int guestCount,
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

        var partySize = guestCount <= 0 ? 1 : Math.Min(guestCount, ErrorCodes.MaxBookingGuests);
        var allServiceIds = new List<Guid> { serviceId };
        if (additionalServiceIds is { Count: > 0 })
        {
            allServiceIds.AddRange(additionalServiceIds.Where(id => id != serviceId));
        }

        var services = await dbContext.ServiceOfferings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive && allServiceIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (services.Count != allServiceIds.Distinct().Count())
        {
            return Result.Failure<IReadOnlyList<AvailableSlotResponse>>(Error.NotFound("Service"));
        }

        var durationMinutes = services.Sum(x => x.DurationMinutes) * partySize;

        var eligibleStaffIds = await GetEligibleStaffIdsAsync(tenantId, serviceId, cancellationToken);
        foreach (var extraId in allServiceIds.Where(id => id != serviceId))
        {
            var extraEligible = await GetEligibleStaffIdsAsync(tenantId, extraId, cancellationToken);
            if (eligibleStaffIds.Count == 0)
            {
                eligibleStaffIds = extraEligible;
            }
            else if (extraEligible.Count > 0)
            {
                eligibleStaffIds = eligibleStaffIds.Intersect(extraEligible).ToArray();
            }
        }

        if (staffMemberId is Guid requestedStaffId)
        {
            var staff = await dbContext.StaffMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == requestedStaffId && x.TenantId == tenantId && x.IsActive,
                    cancellationToken);

            if (staff is null)
            {
                return Result.Failure<IReadOnlyList<AvailableSlotResponse>>(
                    ErrorCodes.StaffUnavailableError());
            }

            if (eligibleStaffIds.Count > 0 && !eligibleStaffIds.Contains(requestedStaffId))
            {
                return Result.Failure<IReadOnlyList<AvailableSlotResponse>>(
                    ErrorCodes.StaffNotEligibleError());
            }
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
            .GenerateSlotStarts(schedulingTimeZone, rules, rangeStart, rangeEnd, durationMinutes)
            .Where(start => start > DateTimeOffset.UtcNow)
            .Where(start =>
            {
                var end = start.AddMinutes(durationMinutes);
                return BookingConflictChecker.IsStaffSlotAvailable(
                    existing,
                    eligibleStaffIds,
                    start,
                    end,
                    staffMemberId);
            })
            .Select(start => new AvailableSlotResponse(
                start,
                start.AddMinutes(durationMinutes)))
            .ToArray();

        return Result.Success<IReadOnlyList<AvailableSlotResponse>>(slots);
    }

    /// <summary>
    /// Active staff who can perform the service. Empty service links = eligible for all services.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> GetEligibleStaffIdsAsync(
        Guid tenantId,
        Guid serviceId,
        CancellationToken cancellationToken)
    {
        var staff = await dbContext.StaffMembers
            .AsNoTracking()
            .Include(x => x.ServiceLinks)
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .ToListAsync(cancellationToken);

        return staff
            .Where(x =>
                x.ServiceLinks.Count == 0
                || x.ServiceLinks.Any(link => link.ServiceOfferingId == serviceId))
            .Select(x => x.Id)
            .ToArray();
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
