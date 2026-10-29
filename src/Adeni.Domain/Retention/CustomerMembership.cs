namespace Adeni.Domain.Retention;

using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;

/// <summary>
/// A roster customer's enrolment in a <see cref="MembershipPlan"/> (Sprint 37.2-B). Plan terms are snapshotted at
/// enrolment so later plan edits never rewrite an enrolled member. Credits are granted per cycle through the
/// 37.1 benefit ledger (<see cref="BenefitBalance"/>); no money or stored value is ever held here.
/// </summary>
public sealed class CustomerMembership : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid TenantCustomerId { get; set; }

    public Guid MembershipPlanId { get; set; }

    public Guid? EnrolledAtLocationId { get; set; }

    public MembershipEnrolChannel EnrolChannel { get; set; }

    public CustomerMembershipStatus Status { get; set; }

    public string NameSnapshot { get; set; } = string.Empty;

    public decimal PriceSnapshot { get; set; }

    public string CurrencySnapshot { get; set; } = "NGN";

    public MembershipBillingInterval BillingIntervalSnapshot { get; set; }

    public MembershipRolloverPolicy RolloverPolicySnapshot { get; set; }

    public MembershipCancellationPolicy CancellationPolicySnapshot { get; set; }

    public bool AllowFreezeSnapshot { get; set; }

    public int PastDueGraceDaysSnapshot { get; set; }

    public string? Note { get; set; }

    public string EnrolledBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    /// <summary>1-based billing cycle currently in force; 0 while <see cref="CustomerMembershipStatus.Pending"/>.</summary>
    public int CurrentCycleNumber { get; set; }

    public DateTimeOffset? CurrentCycleStart { get; set; }

    /// <summary>End of the paid cycle; renewal (slice C) happens here.</summary>
    public DateTimeOffset? CurrentCycleEnd { get; set; }

    public DateTimeOffset? FrozenAt { get; set; }

    public DateTimeOffset? CancelRequestedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public string? CancelledBy { get; set; }

    public string? CancelReason { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<CustomerMembershipBenefit> Benefits { get; set; } = new List<CustomerMembershipBenefit>();

    public ICollection<BenefitBalance> Balances { get; set; } = new List<BenefitBalance>();

    /// <summary>Statuses that still occupy the customer's slot on a plan (one live membership per customer + plan).</summary>
    public static bool IsLive(CustomerMembershipStatus status) =>
        status is CustomerMembershipStatus.Pending or CustomerMembershipStatus.Active
            or CustomerMembershipStatus.PastDue or CustomerMembershipStatus.Frozen
            or CustomerMembershipStatus.CancelPending;

    public static DateTimeOffset AddCycle(DateTimeOffset from, MembershipBillingInterval interval) =>
        interval switch
        {
            MembershipBillingInterval.Quarterly => from.AddMonths(3),
            MembershipBillingInterval.Annually => from.AddYears(1),
            _ => from.AddMonths(1),
        };

    /// <summary>Starts cycle 1 at <paramref name="at"/> (no pro-rating). Pending → Active.</summary>
    public void Activate(DateTimeOffset at)
    {
        Status = CustomerMembershipStatus.Active;
        ActivatedAt = at;
        CurrentCycleNumber = 1;
        CurrentCycleStart = at;
        CurrentCycleEnd = AddCycle(at, BillingIntervalSnapshot);
    }

    /// <summary>Active → Frozen (only when the plan snapshot allows it).</summary>
    public Result Freeze(DateTimeOffset at)
    {
        if (!AllowFreezeSnapshot)
        {
            return Result.Failure(RetentionErrorCodes.MembershipFreezeNotAllowedError());
        }

        if (Status != CustomerMembershipStatus.Active)
        {
            return Result.Failure(RetentionErrorCodes.MembershipInvalidStateError());
        }

        Status = CustomerMembershipStatus.Frozen;
        FrozenAt = at;
        return Result.Success();
    }

    /// <summary>Frozen → Active within the same paid cycle.</summary>
    public Result Unfreeze()
    {
        if (Status != CustomerMembershipStatus.Frozen)
        {
            return Result.Failure(RetentionErrorCodes.MembershipInvalidStateError());
        }

        Status = CustomerMembershipStatus.Active;
        FrozenAt = null;
        return Result.Success();
    }

    /// <summary>
    /// Applies the plan's cancellation policy: <c>Immediate</c> (or a membership that never activated) ends now;
    /// <c>EndOfCycle</c> keeps the paid cycle usable until <see cref="CurrentCycleEnd"/>. Returns true when the
    /// membership ended immediately (caller forfeits remaining credits).
    /// </summary>
    public Result<bool> RequestCancel(string actor, string? reason, DateTimeOffset at)
    {
        if (!IsLive(Status))
        {
            return Result.Failure<bool>(RetentionErrorCodes.MembershipInvalidStateError());
        }

        CancelRequestedAt = at;
        CancelledBy = actor;
        CancelReason = reason;

        if (Status == CustomerMembershipStatus.Pending
            || CancellationPolicySnapshot == MembershipCancellationPolicy.Immediate)
        {
            Status = CustomerMembershipStatus.Cancelled;
            CancelledAt = at;
            return Result.Success(true);
        }

        Status = CustomerMembershipStatus.CancelPending;
        FrozenAt = null;
        return Result.Success(false);
    }

    public override string ToString() => $"CustomerMembership {Id} {Status}";
}

/// <summary>Plan benefit line snapshotted at enrolment; later cycles grant from these, not the live plan.</summary>
public sealed class CustomerMembershipBenefit : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CustomerMembershipId { get; set; }

    public Guid ServiceOfferingId { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public int QuantityPerCycle { get; set; }

    public int SortOrder { get; set; }
}
