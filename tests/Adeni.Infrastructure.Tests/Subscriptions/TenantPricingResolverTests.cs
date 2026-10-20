namespace Adeni.Infrastructure.Tests.Subscriptions;

using Adeni.Application.Abstractions;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Catalog;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class TenantPricingResolverTests
{
    [Fact]
    public void ResolveForTenant_uses_custom_price_when_valid()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Custom",
            SubscriptionSku = SubscriptionSku.Professional,
            CustomSaasMonthlyAmount = 40_000m,
            CustomSaasCurrency = "NGN",
        };

        var resolver = CreateResolver(BuildDb());
        var result = resolver.ResolveForTenant(tenant, "NG");

        Assert.Equal(55_000m, result.CatalogListPriceMajor);
        Assert.Equal(40_000m, result.ListPriceMajor);
        Assert.Equal(40_000m, result.EffectiveMonthlyAmountMajor);
        Assert.Equal("NGN", result.Currency);
        Assert.Equal(PricingCatalogMetadata.ActiveVersionCode, result.PricingCatalogVersion);
    }

    [Fact]
    public void ResolveForTenant_applies_percent_discount_to_list_price()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Discount",
            SubscriptionSku = SubscriptionSku.Professional,
            CustomSaasMonthlyAmount = 50_000m,
            CustomSaasCurrency = "NGN",
            DiscountPercent = 10m,
            DiscountCyclesRemaining = 3,
        };

        var resolver = CreateResolver(BuildDb());
        var result = resolver.ResolveForTenant(tenant, "NG");

        Assert.Equal(50_000m, result.ListPriceMajor);
        Assert.Equal(45_000m, result.EffectiveMonthlyAmountMajor);
        Assert.Equal(45_000L, resolver.ToAmountMinor(result.EffectiveMonthlyAmountMajor, result.Currency));
    }

    [Fact]
    public void ResolveForTenant_falls_back_to_catalog_when_no_custom()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Catalog",
            SubscriptionSku = SubscriptionSku.Business,
        };

        var resolver = CreateResolver(BuildDb());
        var result = resolver.ResolveForTenant(tenant, "NG");

        Assert.Equal(95_000m, result.CatalogListPriceMajor);
        Assert.Equal(95_000m, result.ListPriceMajor);
        Assert.Equal(95_000m, result.EffectiveMonthlyAmountMajor);
    }

    [Fact]
    public async Task ResolvePrimaryBillingCountryAsync_uses_primary_location_market()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var resolver = scope.ServiceProvider.GetRequiredService<ITenantPricingResolver>();

        var tenantId = Guid.NewGuid();
        db.CatalogMarkets.Add(new CatalogMarket
        {
            Id = "ottawa",
            Name = "Ottawa",
            CountryCode = "CA",
            Currency = "CAD",
            TimeZoneId = "America/Toronto",
            IsLive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "CA tenant",
            SubscriptionSku = SubscriptionSku.Starter,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = "ca-main",
            Name = "Main",
            MarketId = "ottawa",
            AddressLine = "1 Street",
            Area = "Centretown",
            TimeZoneId = "America/Toronto",
            IsPrimary = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var country = await resolver.ResolvePrimaryBillingCountryAsync(tenantId);

        Assert.Equal("CA", country);
    }

    private static AdeniDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<AdeniDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AdeniDbContext(options, new Adeni.Infrastructure.Context.TenantContext());
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddScoped<IPricingCatalogService, PricingCatalogService>();
        services.AddScoped<ITenantPricingResolver, TenantPricingResolver>();
        return services.BuildServiceProvider();
    }

    private static TenantPricingResolver CreateResolver(AdeniDbContext db)
    {
        var catalog = new PricingCatalogService(
            db,
            new Adeni.Infrastructure.Auditing.InMemoryAuditLogWriter(),
            new ResolverTestCorrelation());
        return new TenantPricingResolver(db, catalog);
    }

    private sealed class ResolverTestCorrelation : ICorrelationContext
    {
        public string CorrelationId { get; } = "test";

        public void Set(string correlationId) { }
    }
}
