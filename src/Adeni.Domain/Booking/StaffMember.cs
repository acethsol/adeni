namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

/// <summary>Tenant-managed practitioner shown in booking (owner-managed roster; no Auth0 login).</summary>
public sealed class StaffMember : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>Public booking label (defaults to First + Last).</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>One of <see cref="StaffRoleKeys"/>.</summary>
    public string RoleKey { get; set; } = StaffRoleKeys.Other;

    public string? Title { get; set; }

    public string? Bio { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public string? AvatarImageKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<StaffServiceLink> ServiceLinks { get; set; } = new List<StaffServiceLink>();

    public ICollection<StaffWeeklyAvailability> WeeklyHours { get; set; } = new List<StaffWeeklyAvailability>();

    public ICollection<StaffLeave> LeaveEntries { get; set; } = new List<StaffLeave>();
}
