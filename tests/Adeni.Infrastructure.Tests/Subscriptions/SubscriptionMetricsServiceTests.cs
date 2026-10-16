namespace Adeni.Infrastructure.Tests.Subscriptions;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Catalog;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class SubscriptionMetricsServiceTests
{
    [Fact]
    public async Task GetAdminMetricsAsync_uses_synced_charges_when_present()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var metrics = scope.ServiceProvider.GetRequiredService<ISubscriptionMetricsService>();

        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Paying",
            Status = TenantStatus.Verified,
            SubscriptionSku = SubscriptionSku.Professional,
            BillingStatus = TenantBillingStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.TenantSubscriptionCharges.Add(new TenantSubscriptionCharge
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IdempotencyKey = "subscription.create:ref1",
            ProviderReference = "ref1",
            EventType = "subscription.create",
            SubscriptionSku = SubscriptionSku.Professional,
            AmountMinor = 70_000,
            Currency = "NGN",
            Status = "active",
            OccurredAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await metrics.GetAdminMetricsAsync();

        Assert.Equal(1, result.PaidTenantCount);
        Assert.Equal("synced_charges", result.MrrSource);
        Assert.Single(result.SyncedMrrByCurrency);
        Assert.Equal(70_000, result.SyncedMrrByCurrency[0].AmountMinor);
    }

    [Fact]
    public async Task GetAdminMetricsAsync_estimates_mrr_from_list_prices_when_no_charges()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var metrics = scope.ServiceProvider.GetRequiredService<ISubscriptionMetricsService>();

        var tenantId = Guid.NewGuid();
        db.CatalogMarkets.Add(new CatalogMarket
        {
            Id = "lagos",
            Name = "Lagos",
            CountryCode = "NG",
            Currency = "NGN",
            TimeZoneId = "Africa/Lagos",
            IsLive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Paying",
            Status = TenantStatus.Verified,
            SubscriptionSku = SubscriptionSku.Business,
            BillingStatus = TenantBillingStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = "loc",
            Name = "Main",
            MarketId = "lagos",
            AddressLine = "1 Road",
            Area = "Area",
            TimeZoneId = "Africa/Lagos",
            IsPrimary = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await metrics.GetAdminMetricsAsync();

        Assert.Equal("estimated_list_price", result.MrrSource);
        Assert.Single(result.EstimatedMrrByCurrency);
        Assert.Equal(100_000, result.EstimatedMrrByCurrency[0].AmountMinor);
        Assert.Equal("NGN", result.EstimatedMrrByCurrency[0].Currency);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddScoped<ISubscriptionMetricsService, SubscriptionMetricsService>();
        return services.BuildServiceProvider();
    }
}
