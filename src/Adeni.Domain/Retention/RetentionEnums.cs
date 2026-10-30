namespace Adeni.Domain.Retention;

/// <summary>Where a sold package may be redeemed (approved R1-A #6). Persisted as int.</summary>
public enum PackageRedemptionScope
{
    AllLocations = 0,
    PurchaseLocationOnly = 1,
    SelectedLocations = 2,
}

/// <summary>Which locations may sell a package. Persisted as int.</summary>
public enum PackageSellScope
{
    AllLocations = 0,
    SelectedLocations = 1,
}

public enum CustomerPackageStatus
{
    PendingPayment = 0,
    Active = 1,
    Expired = 2,
    Voided = 3,
}

public enum PackageSoldChannel
{
    InPersonTill = 0,
    InPersonTransfer = 1,
    Online = 2,
}

public enum BenefitTransactionKind
{
    Grant = 0,
    Reserve = 1,
    Release = 2,
    Redeem = 3,
    Forfeit = 4,
    Void = 5,
    Adjust = 6,
}

/// <summary>Membership billing cadence. Persisted as int.</summary>
public enum MembershipBillingInterval
{
    Monthly = 0,
    Quarterly = 1,
    Annually = 2,
}

/// <summary>What happens to unused cycle credits at renewal. Persisted as int.</summary>
public enum MembershipRolloverPolicy
{
    None = 0,
    CarryOverOneCycle = 1,
}

/// <summary>Customer membership lifecycle. Persisted as int.</summary>
public enum CustomerMembershipStatus
{
    Pending = 0,
    Active = 1,
    PastDue = 2,
    Frozen = 3,
    CancelPending = 4,
    Cancelled = 5,
    Expired = 6,
}

/// <summary>How a membership was enrolled. Persisted as int. <c>Online</c> arrives with slice C/D.</summary>
public enum MembershipEnrolChannel
{
    InPersonTill = 0,
    InPersonTransfer = 1,
    Online = 2,
}

/// <summary>
/// Loyalty ledger vocabulary (Sprint 37.3). Persisted as int. Slice A writes only <c>Adjust</c>; <c>Earn</c> /
/// <c>Redeem</c> / <c>Expire</c> are reserved for slices B–C so the check constraint never has to change.
/// </summary>
public enum LoyaltyTransactionKind
{
    Earn = 0,
    Redeem = 1,
    Adjust = 2,
    Expire = 3,
}

/// <summary>Reward catalog entry type (Sprint 37.3-D). Persisted as int.</summary>
public enum LoyaltyRewardKind
{
    FreeService = 0,
}

/// <summary>When a member-requested cancellation takes effect. Persisted as int.</summary>
public enum MembershipCancellationPolicy
{
    EndOfCycle = 0,
    Immediate = 1,
}

/// <summary>Which charge of a membership a <see cref="MembershipPayment"/> row tracks. Persisted as int.</summary>
public enum MembershipPaymentKind
{
    Initial = 0,
    Renewal = 1,
}

/// <summary>
/// Membership charge outcome. <c>Unapplied</c> = the provider confirmed the payment but the membership could no
/// longer take it (cancelled / expired) — flagged for manual refund review, never silently granted. Persisted as int.
/// </summary>
public enum MembershipPaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Unapplied = 3,
}
