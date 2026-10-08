namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

public sealed class StaffLeave : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid StaffMemberId { get; set; }

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    /// <summary>Owner-only note; not shown on public APIs.</summary>
    public string? Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public StaffMember? StaffMember { get; set; }
}
