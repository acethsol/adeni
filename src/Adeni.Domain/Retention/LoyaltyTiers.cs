namespace Adeni.Domain.Retention;

using Adeni.Domain.Tenancy;

/// <summary>Tenant-configured loyalty tier (Sprint 37.3-D). Threshold uses lifetime earned points only.</summary>
public sealed class LoyaltyTier : ITenantEntity
{
    public const int MaxNameLength = 120;

    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int MinLifetimeEarned { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Catalog reward beyond checkout discount (Sprint 37.3-D).</summary>
public sealed class LoyaltyReward : ITenantEntity
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 500;
    public const int MaxPointsCost = 1_000_000;

    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public LoyaltyRewardKind Kind { get; set; } = LoyaltyRewardKind.FreeService;

    public int PointsCost { get; set; }

    public Guid? ServiceOfferingId { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class LoyaltyRewardRedemption : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid LoyaltyAccountId { get; set; }

    public Guid TenantCustomerId { get; set; }

    public Guid LoyaltyRewardId { get; set; }

    public Guid LoyaltyTransactionId { get; set; }

    public int PointsSpent { get; set; }

    public DateTimeOffset IssuedAt { get; set; }
}
