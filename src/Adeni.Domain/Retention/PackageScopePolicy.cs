namespace Adeni.Domain.Retention;

/// <summary>Pure scope rules (approved R1-A #6). Customer home location is never consulted.</summary>
public static class PackageScopePolicy
{
    public static bool CanSellAt(
        PackageSellScope scope,
        IReadOnlyCollection<Guid> selectedLocationIds,
        Guid? locationId) =>
        scope switch
        {
            PackageSellScope.AllLocations => true,
            PackageSellScope.SelectedLocations => locationId is Guid id && selectedLocationIds.Contains(id),
            _ => false,
        };

    public static bool CanRedeemAt(
        PackageRedemptionScope scope,
        IReadOnlyCollection<Guid> selectedLocationIds,
        Guid? soldAtLocationId,
        Guid? bookingLocationId) =>
        scope switch
        {
            PackageRedemptionScope.AllLocations => true,
            PackageRedemptionScope.PurchaseLocationOnly =>
                soldAtLocationId is Guid sold && bookingLocationId is Guid booked && sold == booked,
            PackageRedemptionScope.SelectedLocations =>
                bookingLocationId is Guid booked && selectedLocationIds.Contains(booked),
            _ => false,
        };
}