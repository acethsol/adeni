namespace Adeni.Domain.Tenancy;

/// <summary>Tiered trust badge grant or upgrade request for a tenant.</summary>
public sealed class TenantVerificationBadge : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public VerificationBadgeType BadgeType { get; set; }

    public VerificationBadgeStatus Status { get; set; }

    public string? ReferenceNumber { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset? GrantedAt { get; set; }

    public string? GrantedByAdminId { get; set; }
}
