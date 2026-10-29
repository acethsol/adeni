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

    /// <summary>Billing provider that holds the mandate / subscription (37.2-C). References only — never card data or balances.</summary>
    public string? ProviderName { get; set; }

    public string? ProviderMandateRef { get; set; }

    public string? ProviderSubscriptionRef { get; set; }

    /// <summary>When the unpaid renewal started (= the ended cycle's end); grace is measured from here.</summary>
    public DateTimeOffset? PastDueSince { get; set; }

    /// <summary>Consecutive failed/unpaid renewal attempts; reset on a paid renewal.</summary>
    public int FailedPaymentCount { get; set; }

    public DateTimeOffset? ExpiredAt { get; set; }

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

    /// <summary>
    /// Frozen → Active. The paid cycle is extended by the time spent frozen so a freeze never burns paid days
    /// (the caller moves the current-cycle balances' expiry to the new <see cref="CurrentCycleEnd"/>).
    /// </summary>
    public Result Unfreeze(DateTimeOffset at)
    {
        if (Status != CustomerMembershipStatus.Frozen)
        {
            return Result.Failure(RetentionErrorCodes.MembershipInvalidStateError());
        }

        if (FrozenAt is { } frozenAt && CurrentCycleEnd is { } end && at > frozenAt)
        {
            CurrentCycleEnd = end + (at - frozenAt);
        }

        Status = CustomerMembershipStatus.Active;
        FrozenAt = null;
        return Result.Success();
    }

    /// <summary>Online memberships are billed through a provider; in-person ones are renewed by staff recording a till/transfer payment.</summary>
    public bool IsOnline => EnrolChannel == MembershipEnrolChannel.Online;

    public TimeSpan GracePeriod => TimeSpan.FromDays(Math.Max(0, PastDueGraceDaysSnapshot));

    /// <summary>
    /// Whether this cycle's credits may be reserved at <paramref name="at"/>: paid cycle in force, or the plan's
    /// past-due grace after it. Frozen / pending / terminal memberships never redeem.
    /// </summary>
    public bool CanRedeem(DateTimeOffset at)
    {
        if (CurrentCycleEnd is not { } end)
        {
            return false;
        }

        return Status switch
        {
            CustomerMembershipStatus.Active or CustomerMembershipStatus.PastDue => at <= end + GracePeriod,
            CustomerMembershipStatus.CancelPending => at < end,
            _ => false,
        };
    }

    /// <summary>True when the paid cycle has ended and the plan's grace has also run out.</summary>
    public bool GraceElapsed(DateTimeOffset at) =>
        CurrentCycleEnd is { } end && at > end + GracePeriod;

    /// <summary>Active → PastDue when a cycle ends unpaid (or a renewal charge fails). Idempotent once past due.</summary>
    public bool MarkPastDue()
    {
        if (Status != CustomerMembershipStatus.Active)
        {
            return false;
        }

        Status = CustomerMembershipStatus.PastDue;
        PastDueSince ??= CurrentCycleEnd;
        return true;
    }

    /// <summary>
    /// A paid renewal starts the next cycle. Continuous with the previous one (<see cref="CurrentCycleEnd"/>) unless
    /// that already lapsed, in which case the cycle starts at <paramref name="at"/>. Active / PastDue only.
    /// </summary>
    public Result ApplyRenewal(DateTimeOffset at)
    {
        if (Status is not (CustomerMembershipStatus.Active or CustomerMembershipStatus.PastDue))
        {
            return Result.Failure(RetentionErrorCodes.MembershipInvalidStateError());
        }

        var start = CurrentCycleEnd ?? at;
        var end = AddCycle(start, BillingIntervalSnapshot);
        if (end <= at)
        {
            start = at;
            end = AddCycle(at, BillingIntervalSnapshot);
        }

        Status = CustomerMembershipStatus.Active;
        CurrentCycleNumber++;
        CurrentCycleStart = start;
        CurrentCycleEnd = end;
        PastDueSince = null;
        FailedPaymentCount = 0;
        return Result.Success();
    }

    /// <summary>PastDue → Expired once grace has run out without payment.</summary>
    public Result Expire(DateTimeOffset at)
    {
        if (Status != CustomerMembershipStatus.PastDue)
        {
            return Result.Failure(RetentionErrorCodes.MembershipInvalidStateError());
        }

        Status = CustomerMembershipStatus.Expired;
        ExpiredAt = at;
        return Result.Success();
    }

    /// <summary>CancelPending → Cancelled at the end of the paid cycle.</summary>
    public Result CompleteCancellation(DateTimeOffset at)
    {
        if (Status != CustomerMembershipStatus.CancelPending)
        {
            return Result.Failure(RetentionErrorCodes.MembershipInvalidStateError());
        }

        Status = CustomerMembershipStatus.Cancelled;
        CancelledAt = at;
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
