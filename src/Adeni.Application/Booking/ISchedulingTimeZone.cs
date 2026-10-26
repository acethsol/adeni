namespace Adeni.Application.Booking;

public interface ISchedulingTimeZone
{
    string TimeZoneId { get; }

    bool IsConfiguredZoneValid => true;

    DateTimeOffset ToLocal(DateTimeOffset instant);

    DateTimeOffset ToUtc(DateTime date, TimeOnly time);

    bool IsValidLocalTime(DateTime date, TimeOnly time) => true;
}

public interface ITenantSchedulingTimeZone
{
    Task<ISchedulingTimeZone> ForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ISchedulingTimeZone> ForLocationAsync(
        Guid locationId,
        CancellationToken cancellationToken = default);
}
