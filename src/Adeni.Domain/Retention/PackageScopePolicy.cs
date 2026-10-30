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

    /// <summary>True when both locations are known and differ (37.4 plan gate — not product scope).</summary>
    public static bool IsCrossLocationRedeemAttempt(Guid? originLocationId, Guid? bookingLocationId) =>
        originLocationId is Guid origin && bookingLocationId is Guid booked && origin != booked;
}