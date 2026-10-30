namespace Adeni.Domain.Retention;

using Adeni.Domain.Tenancy;

/// <summary>Tenant referral program settings (Sprint 37.3-E). Rewards are loyalty points, not cash.</summary>
public sealed class ReferralProgram : ITenantEntity
{
    public const int MaxRewardPoints = 1_000_000;

    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public bool IsEnabled { get; set; }

    public int ReferrerRewardPoints { get; set; }

    public int RefereeRewardPoints { get; set; }

    /// <summary>Cap on successful referrals per referrer; null = unlimited.</summary>
    public int? MaxRewardsPerReferrer { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ReferralCode : ITenantEntity
{
    public const int MaxCodeLength = 32;
    public const int MinCodeLength = 4;

    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid TenantCustomerId { get; set; }

    public string Code { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

public enum ReferralAttributionStatus
{
    Pending = 0,
    Qualified = 1,
    Rewarded = 2,
    Rejected = 3,
}

public sealed class ReferralAttribution : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ReferrerTenantCustomerId { get; set; }

    public Guid RefereeTenantCustomerId { get; set; }

    public Guid ReferralCodeId { get; set; }

    public Guid? SourceBookingId { get; set; }

    public Guid? QualifiedBookingId { get; set; }

    public ReferralAttributionStatus Status { get; set; }

    public string? RejectReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? QualifiedAt { get; set; }

    public DateTimeOffset? RewardedAt { get; set; }
}
