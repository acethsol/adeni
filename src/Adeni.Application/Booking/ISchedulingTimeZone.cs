namespace Adeni.Application.Booking;

public interface ISchedulingTimeZone
{
    string TimeZoneId { get; }

    DateTimeOffset ToLocal(DateTimeOffset instant);

    DateTimeOffset ToUtc(DateTime date, TimeOnly time);
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
