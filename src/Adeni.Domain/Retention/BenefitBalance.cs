namespace Adeni.Domain.Retention;

using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;

/// <summary>
/// Mutable counters for one benefit line of a customer package. Invariant:
/// <c>Used + Reserved + Forfeited &lt;= Granted</c>; every change appends a <see cref="BenefitTransaction"/>.
/// </summary>
public sealed class BenefitBalance : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CustomerPackageId { get; set; }

    public Guid ServiceOfferingId { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public int Granted { get; set; }

    public int Used { get; set; }

    public int Reserved { get; set; }

    public int Forfeited { get; set; }

    /// <summary>Last ledger sequence written for this balance.</summary>
    public int Sequence { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public int Remaining => Granted - Used - Reserved - Forfeited;

    public static BenefitBalance Grant(
        Guid tenantId,
        Guid customerPackageId,
        Guid serviceOfferingId,
        string serviceName,
        int quantity,
        string actor,
        Guid? locationId,
        DateTimeOffset at,
        out BenefitTransaction transaction)
    {
        var balance = new BenefitBalance
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerPackageId = customerPackageId,
            ServiceOfferingId = serviceOfferingId,
            ServiceName = serviceName,
            Granted = quantity,
        };
        transaction = balance.Append(BenefitTransactionKind.Grant, quantity, quantity, null, locationId, actor, null, at);
        return balance;
    }

    public Result<BenefitTransaction> Reserve(
        int quantity, Guid bookingId, Guid? locationId, string actor, DateTimeOffset at)
    {
        if (quantity <= 0)
        {
            return Result.Failure<BenefitTransaction>(RetentionErrorCodes.ValidationError("Quantity must be positive."));
        }

        if (Remaining < quantity)
        {
            return Result.Failure<BenefitTransaction>(RetentionErrorCodes.BenefitInsufficientError());
        }

        Reserved += quantity;
        return Result.Success(Append(BenefitTransactionKind.Reserve, quantity, -quantity, bookingId, locationId, actor, null, at));
    }

    public Result<BenefitTransaction> Release(
        int quantity, Guid? bookingId, Guid? locationId, string actor, string? reason, DateTimeOffset at)
    {
        if (quantity <= 0 || Reserved < quantity)
        {
            return Result.Failure<BenefitTransaction>(RetentionErrorCodes.BenefitConflictError());
        }

        Reserved -= quantity;
        return Result.Success(Append(BenefitTransactionKind.Release, quantity, quantity, bookingId, locationId, actor, reason, at));
    }

    public Result<BenefitTransaction> Redeem(
        int quantity, Guid? bookingId, Guid? locationId, string actor, DateTimeOffset at)
    {
        if (quantity <= 0 || Reserved < quantity)
        {
            return Result.Failure<BenefitTransaction>(RetentionErrorCodes.BenefitConflictError());
        }

        Reserved -= quantity;
        Used += quantity;
        return Result.Success(Append(BenefitTransactionKind.Redeem, quantity, 0, bookingId, locationId, actor, null, at));
    }

    /// <summary>Forfeits all unreserved credits. Returns null when nothing remains.</summary>
    public BenefitTransaction? ForfeitRemaining(
        BenefitTransactionKind kind, string actor, string? reason, DateTimeOffset at)
    {
        var quantity = Remaining;
        if (quantity <= 0)
        {
            return null;
        }

        Forfeited += quantity;
        return Append(kind, quantity, -quantity, null, null, actor, reason, at);
    }

    private BenefitTransaction Append(
        BenefitTransactionKind kind,
        int quantity,
        int delta,
        Guid? bookingId,
        Guid? locationId,
        string actor,
        string? reason,
        DateTimeOffset at)
    {
        Sequence++;
        return new BenefitTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            BenefitBalanceId = Id,
            Sequence = Sequence,
            Kind = kind,
            Quantity = quantity,
            Delta = delta,
            ReservedAfter = Reserved,
            UsedAfter = Used,
            RemainingAfter = Remaining,
            BookingId = bookingId,
            BusinessLocationId = locationId,
            Actor = actor,
            Reason = reason,
            OccurredAt = at,
        };
    }
}