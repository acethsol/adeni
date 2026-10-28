namespace Adeni.Domain.Tests.Retention;

using Adeni.Domain.Retention;

public sealed class PackageScopePolicyTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    [Fact]
    public void CanSellAt_AllLocations_AllowsAnything() =>
        Assert.True(PackageScopePolicy.CanSellAt(PackageSellScope.AllLocations, [], A));

    [Fact]
    public void CanSellAt_Selected_OnlyListedLocations()
    {
        Assert.True(PackageScopePolicy.CanSellAt(PackageSellScope.SelectedLocations, [A], A));
        Assert.False(PackageScopePolicy.CanSellAt(PackageSellScope.SelectedLocations, [A], B));
        Assert.False(PackageScopePolicy.CanSellAt(PackageSellScope.SelectedLocations, [A], null));
    }

    [Fact]
    public void CanRedeemAt_AllLocations_AllowsAnything() =>
        Assert.True(PackageScopePolicy.CanRedeemAt(PackageRedemptionScope.AllLocations, [], A, B));

    [Fact]
    public void CanRedeemAt_PurchaseLocationOnly_RequiresSameLocation()
    {
        Assert.True(PackageScopePolicy.CanRedeemAt(PackageRedemptionScope.PurchaseLocationOnly, [], A, A));
        Assert.False(PackageScopePolicy.CanRedeemAt(PackageRedemptionScope.PurchaseLocationOnly, [], A, B));
        Assert.False(PackageScopePolicy.CanRedeemAt(PackageRedemptionScope.PurchaseLocationOnly, [], A, null));
    }

    [Fact]
    public void CanRedeemAt_Selected_OnlyListedBookingLocations()
    {
        Assert.True(PackageScopePolicy.CanRedeemAt(PackageRedemptionScope.SelectedLocations, [B], A, B));
        Assert.False(PackageScopePolicy.CanRedeemAt(PackageRedemptionScope.SelectedLocations, [B], A, A));
    }
}
