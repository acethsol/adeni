namespace Adeni.Domain.Retention;

using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;

/// <summary>
/// Tenant-wide loyalty program settings (Sprint 37.3-A). One row per tenant. Points are program credits of this
/// business only — never cash, never transferable. Earn/redeem/expiry values are typed columns (no rules DSL);
/// slice A stores them, slices B–C apply them.
/// </summary>
public sealed class LoyaltyProgram : ITenantEntity
{
    public const int MaxEarnPoints = 1000;
    public const int MaxRedeemPoints = 1_000_000;
    public const int MaxExpiryMonths = 60;
    public const decimal MaxAmount = 9_999_999_999.99m;

    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public bool IsEnabled { get; set; }

    public string Currency { get; set; } = "NGN";

    /// <summary>Earn <see cref="EarnPoints"/> points for every <see cref="EarnPerAmount"/> spent.</summary>
    public int EarnPoints { get; set; }

    public decimal EarnPerAmount { get; set; }

    /// <summary><see cref="RedeemPoints"/> points are worth <see cref="RedeemValue"/> off a service price.</summary>
    public int RedeemPoints { get; set; }

    public decimal RedeemValue { get; set; }

    public int MinRedeemPoints { get; set; }

    /// <summary>Months until earned points expire; null = never.</summary>
    public int? PointsExpiryMonths { get; set; }

    public string UpdatedBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Points balance of one roster customer at one tenant. Mutable counters guarded by <c>rowversion</c>; every change
/// appends an immutable <see cref="LoyaltyTransaction"/>. Invariant: <c>PointsBalance &gt;= 0</c>.
/// </summary>
public sealed class LoyaltyAccount : ITenantEntity
{
    public const int MaxAdjustPoints = 1_000_000;

    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid TenantCustomerId { get; set; }

    public int PointsBalance { get; set; }

    /// <summary>Sum of <c>Earn</c> rows only (tier metric, slice D). Manual adjustments never count.</summary>
    public int LifetimeEarned { get; set; }

    public int LifetimeRedeemed { get; set; }

    /// <summary>Last ledger sequence written for this account.</summary>
    public int Sequence { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public static LoyaltyAccount Open(Guid tenantId, Guid tenantCustomerId, DateTimeOffset at) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenantCustomerId = tenantCustomerId,
            CreatedAt = at,
            UpdatedAt = at,
        };

    /// <summary>Stages a signed manual adjustment. The caller persists the returned ledger row.</summary>
    public Result<LoyaltyTransaction> Adjust(
        int points,
        string reason,
        string actor,
        DateTimeOffset at,
        string? keyHash,
        string? fingerprint)
    {
        if (points == 0 || Math.Abs((long)points) > MaxAdjustPoints)
        {
            return Result.Failure<LoyaltyTransaction>(
                RetentionErrorCodes.ValidationError($"Points must be a non-zero whole number up to {MaxAdjustPoints:N0}."));
        }

        if ((long)PointsBalance + points < 0)
        {
            return Result.Failure<LoyaltyTransaction>(RetentionErrorCodes.LoyaltyInsufficientPointsError());
        }

        PointsBalance += points;
        Sequence++;
        UpdatedAt = at;
        return Result.Success(new LoyaltyTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            LoyaltyAccountId = Id,
            TenantCustomerId = TenantCustomerId,
            Sequence = Sequence,
            Kind = LoyaltyTransactionKind.Adjust,
            Points = points,
            BalanceAfter = PointsBalance,
            Reason = reason,
            Actor = actor,
            KeyHash = keyHash,
            Fingerprint = fingerprint,
            OccurredAt = at,
        });
    }
}

/// <summary>Append-only points ledger row. Never updated or deleted.</summary>
public sealed class LoyaltyTransaction : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid LoyaltyAccountId { get; set; }

    public Guid TenantCustomerId { get; set; }

    public int Sequence { get; set; }

    public LoyaltyTransactionKind Kind { get; set; }

    /// <summary>Signed change to the balance.</summary>
    public int Points { get; set; }

    public int BalanceAfter { get; set; }

    public string? Reason { get; set; }

    /// <summary>Booking that caused an earn / redeem (slices B–C).</summary>
    public Guid? BookingId { get; set; }

    public string Actor { get; set; } = string.Empty;

    /// <summary>Idempotency receipt for manual adjustments; null for system-written rows.</summary>
    public string? KeyHash { get; set; }

    public string? Fingerprint { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
