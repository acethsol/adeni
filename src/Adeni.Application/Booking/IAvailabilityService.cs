namespace Adeni.Application.Booking;

using Adeni.Domain.Common;

public sealed record WeeklyAvailabilityRule(
    DayOfWeek DayOfWeek,
    TimeOnly OpenTime,
    TimeOnly CloseTime);

public sealed record AvailableSlotResponse(
    DateTimeOffset StartAt,
    DateTimeOffset EndAt);

public interface IAvailabilityService
{
    Task<IReadOnlyList<WeeklyAvailabilityRule>> GetWeeklyRulesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WeeklyAvailabilityRule>>> ReplaceWeeklyRulesAsync(
        Guid tenantId,
        IReadOnlyList<WeeklyAvailabilityRule> rules,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AvailableSlotResponse>>> GetAvailableSlotsAsync(
        Guid tenantId,
        Guid serviceId,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AvailableSlotResponse>>> GetAvailableSlotsBySlugAsync(
        string slug,
        Guid serviceId,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        CancellationToken cancellationToken = default);

    Task<bool> IsSlotAvailableAsync(
        Guid tenantId,
        Guid serviceId,
        DateTimeOffset startAt,
        int durationMinutes,
        CancellationToken cancellationToken = default);
}
