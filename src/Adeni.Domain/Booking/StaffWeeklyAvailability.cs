namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

/// <summary>Per-staff weekly hours. Empty set for a member means inherit business hours.</summary>
public sealed class StaffWeeklyAvailability : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid StaffMemberId { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly OpenTime { get; set; }

    public TimeOnly CloseTime { get; set; }

    public StaffMember? StaffMember { get; set; }
}
