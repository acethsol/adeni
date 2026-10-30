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

    /// <summary>Stages a booking earn (slice 37.3-B). Caller persists the ledger row.</summary>
    public Result<LoyaltyTransaction> RecordEarn(int points, Guid bookingId, string actor, DateTimeOffset at)
    {
        if (points <= 0)
        {
            return Result.Failure<LoyaltyTransaction>(
                RetentionErrorCodes.ValidationError("Earn points must be positive."));
        }

        PointsBalance += points;
        LifetimeEarned += points;
        Sequence++;
        UpdatedAt = at;
        return Result.Success(new LoyaltyTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            LoyaltyAccountId = Id,
            TenantCustomerId = TenantCustomerId,
            Sequence = Sequence,
            Kind = LoyaltyTransactionKind.Earn,
            Points = points,
            BalanceAfter = PointsBalance,
            BookingId = bookingId,
            Actor = actor,
            OccurredAt = at,
        });
    }

    /// <summary>Reverses a prior earn for the same booking (cancel/refund policy).</summary>
    public Result<LoyaltyTransaction> RecordEarnReversal(int earnPoints, Guid bookingId, string actor, DateTimeOffset at)
    {
        if (earnPoints <= 0)
        {
            return Result.Failure<LoyaltyTransaction>(RetentionErrorCodes.ValidationError("Invalid earn reversal."));
        }

        var reversal = -earnPoints;
        if ((long)PointsBalance + reversal < 0)
        {
            return Result.Failure<LoyaltyTransaction>(RetentionErrorCodes.LoyaltyInsufficientPointsError());
        }

        PointsBalance += reversal;
        LifetimeEarned = Math.Max(0, LifetimeEarned - earnPoints);
        Sequence++;
        UpdatedAt = at;
        return Result.Success(new LoyaltyTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            LoyaltyAccountId = Id,
            TenantCustomerId = TenantCustomerId,
            Sequence = Sequence,
            Kind = LoyaltyTransactionKind.Earn,
            Points = reversal,
            BalanceAfter = PointsBalance,
            BookingId = bookingId,
            Reason = "booking_reversal",
            Actor = actor,
            OccurredAt = at,
        });
    }

    /// <summary>Stages a booking redemption (slice 37.3-C). Caller persists the ledger row.</summary>
    public Result<LoyaltyTransaction> RecordRedeem(
        int points,
        decimal discountAmount,
        Guid bookingId,
        string actor,
        DateTimeOffset at)
    {
        if (points <= 0 || discountAmount <= 0)
        {
            return Result.Failure<LoyaltyTransaction>(RetentionErrorCodes.ValidationError("Invalid redeem."));
        }

        if (PointsBalance < points)
        {
            return Result.Failure<LoyaltyTransaction>(RetentionErrorCodes.LoyaltyInsufficientPointsError());
        }

        PointsBalance -= points;
        LifetimeRedeemed += points;
        Sequence++;
        UpdatedAt = at;
        return Result.Success(new LoyaltyTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            LoyaltyAccountId = Id,
            TenantCustomerId = TenantCustomerId,
            Sequence = Sequence,
            Kind = LoyaltyTransactionKind.Redeem,
            Points = -points,
            BalanceAfter = PointsBalance,
            DiscountAmount = discountAmount,
            BookingId = bookingId,
            Actor = actor,
            OccurredAt = at,
        });
    }

    /// <summary>Restores points from a booking redeem before the visit is paid (cancel / release).</summary>
    public Result<LoyaltyTransaction> RecordRedeemReversal(
        int redeemedPoints,
        decimal discountAmount,
        Guid bookingId,
        string actor,
        DateTimeOffset at)
    {
        if (redeemedPoints <= 0 || discountAmount <= 0)
        {
            return Result.Failure<LoyaltyTransaction>(RetentionErrorCodes.ValidationError("Invalid redeem reversal."));
        }

        PointsBalance += redeemedPoints;
        LifetimeRedeemed = Math.Max(0, LifetimeRedeemed - redeemedPoints);
        Sequence++;
        UpdatedAt = at;
        return Result.Success(new LoyaltyTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            LoyaltyAccountId = Id,
            TenantCustomerId = TenantCustomerId,
            Sequence = Sequence,
            Kind = LoyaltyTransactionKind.Redeem,
            Points = redeemedPoints,
            BalanceAfter = PointsBalance,
            DiscountAmount = discountAmount,
            BookingId = bookingId,
            Reason = "booking_reversal",
            Actor = actor,
            OccurredAt = at,
        });
    }

    /// <summary>Stages expiry of unused points (slice 37.3-C FIFO sweep).</summary>
    public Result<LoyaltyTransaction> RecordExpire(int points, string actor, DateTimeOffset at)
    {
        if (points <= 0)
        {
            return Result.Failure<LoyaltyTransaction>(RetentionErrorCodes.ValidationError("Expire points must be positive."));
        }

        if (PointsBalance < points)
        {
            return Result.Failure<LoyaltyTransaction>(RetentionErrorCodes.LoyaltyInsufficientPointsError());
        }

        PointsBalance -= points;
        Sequence++;
        UpdatedAt = at;
        return Result.Success(new LoyaltyTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            LoyaltyAccountId = Id,
            TenantCustomerId = TenantCustomerId,
            Sequence = Sequence,
            Kind = LoyaltyTransactionKind.Expire,
            Points = -points,
            BalanceAfter = PointsBalance,
            Reason = "points_expired",
            Actor = actor,
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

    /// <summary>Monetary discount applied for <see cref="LoyaltyTransactionKind.Redeem"/> rows (program currency).</summary>
    public decimal? DiscountAmount { get; set; }

    public string Actor { get; set; } = string.Empty;

    /// <summary>Idempotency receipt for manual adjustments; null for system-written rows.</summary>
    public string? KeyHash { get; set; }

    public string? Fingerprint { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
