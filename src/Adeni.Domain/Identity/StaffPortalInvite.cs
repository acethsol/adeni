namespace Adeni.Domain.Identity;

using Adeni.Domain.Tenancy;

/// <summary>Pending or historical invite for a portal login on an existing tenant.</summary>
public sealed class StaffPortalInvite : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>Portal RBAC role granted on accept (<see cref="PortalPermissionRoles"/>).</summary>
    public string PermissionRole { get; set; } = PortalPermissionRoles.Practitioner;

    public Guid? StaffMemberId { get; set; }

    /// <summary>SHA-256 hex of the raw invite token (raw token is emailed once).</summary>
    public string TokenHash { get; set; } = string.Empty;

    public string Status { get; set; } = StaffPortalInviteStatuses.Pending;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? AcceptedAt { get; set; }

    public string? InvitedByAuth0Sub { get; set; }

    public Tenant? Tenant { get; set; }
}

public static class StaffPortalInviteStatuses
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Revoked = "revoked";
    public const string Expired = "expired";

    public static readonly IReadOnlyList<string> All =
    [
        Pending,
        Accepted,
        Revoked,
        Expired,
    ];
}
