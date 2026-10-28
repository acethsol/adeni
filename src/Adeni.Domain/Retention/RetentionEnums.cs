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