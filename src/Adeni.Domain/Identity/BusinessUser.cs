namespace Adeni.Domain.Identity;

using Adeni.Domain.Tenancy;

public sealed class BusinessUser : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Auth0Sub { get; set; } = string.Empty;

    /// <summary>Portal RBAC role (<see cref="PortalPermissionRoles"/>), not floor staff roleKey.</summary>
    public string Role { get; set; } = PortalPermissionRoles.Owner;

    /// <summary>Optional link to booking roster member (Sprint 25b invite flow).</summary>
    public Guid? StaffMemberId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Tenant? Tenant { get; set; }
}
