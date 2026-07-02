namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

public sealed class WeeklyAvailability : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly OpenTime { get; set; }

    public TimeOnly CloseTime { get; set; }
}
