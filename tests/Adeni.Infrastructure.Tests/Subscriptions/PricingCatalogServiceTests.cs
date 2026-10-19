namespace Adeni.Infrastructure.Tests.Subscriptions;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Subscriptions;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Subscriptions;
using Microsoft.EntityFrameworkCore;

public sealed class PricingCatalogServiceTests
{
    [Fact]
    public async Task EnsureSeededAsync_creates_launch_version()
    {
        await using var db = BuildDb();
        var service = new PricingCatalogService(db);

        await service.EnsureSeededAsync();

        var version = await db.PricingCatalogVersions
            .Include(v => v.Entries)
            .SingleAsync();

        Assert.Equal(PricingCatalogMetadata.ActiveVersionCode, version.Code);
        Assert.Equal(PricingCatalogVersionStatus.Active, version.Status);
        Assert.Equal(9, version.Entries.Count);
    }

    [Fact]
    public async Task GetPublicCountryPricingAsync_returns_ng_prices_after_seed()
    {
        await using var db = BuildDb();
        var service = new PricingCatalogService(db);

        var result = await service.GetPublicCountryPricingAsync("NG");

        Assert.True(result.IsSuccess);
        var pricing = result.Value!;
        Assert.Equal("NG", pricing.CountryCode);
        Assert.Equal(PricingCatalogMetadata.ActiveVersionCode, pricing.PricingCatalogVersion);
        Assert.Contains(pricing.Skus, s => s.Sku == "professional" && s.MonthlyAmountMajor == 55_000m);
    }

    private static AdeniDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AdeniDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AdeniDbContext(options, new TenantContext());
    }
}
