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

/// <summary>When a member-requested cancellation takes effect. Persisted as int.</summary>
public enum MembershipCancellationPolicy
{
    EndOfCycle = 0,
    Immediate = 1,
}
