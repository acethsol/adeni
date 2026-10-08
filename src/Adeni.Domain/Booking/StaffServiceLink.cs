namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

/// <summary>Many-to-many: which services a staff member can perform.</summary>
public sealed class StaffServiceLink : ITenantEntity
{
    public Guid StaffMemberId { get; set; }

    public Guid ServiceOfferingId { get; set; }

    public Guid TenantId { get; set; }

    public StaffMember? StaffMember { get; set; }

    public ServiceOffering? ServiceOffering { get; set; }
}
