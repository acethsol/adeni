namespace Adeni.Infrastructure.Tests.Discovery;

using Adeni.Application.Caching;
using Adeni.Application.Discovery;
using Adeni.Application.Markets;
using Adeni.Application.Reviews;
using Adeni.Application.Storage;
using Adeni.Domain.Booking;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Discovery;
using Adeni.Infrastructure.Markets;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Reviews;
using Adeni.Infrastructure.Tests.Storage;
using Adeni.Infrastructure.Tests.TestData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

public sealed class DiscoveryServiceTests
{
    [Fact]
    public async Task Search_paginates_results()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();

        for (var index = 0; index < 3; index++)
        {
            BusinessTestSeed.SeedVerifiedBusiness(
                db,
                $"shop-{index}",
                $"Shop {index}",
                "barbers",
                $"Area {index}",
                6.4474 + (index * 0.01),
                3.4700);
        }

        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
        var pageOne = await service.SearchAsync(6.4474, 3.4700, null, null, null, 1, 2);
        var pageTwo = await service.SearchAsync(6.4474, 3.4700, null, null, null, 2, 2);

        Assert.True(pageOne.IsSuccess);
        Assert.True(pageTwo.IsSuccess);
        Assert.Equal(3, pageOne.Value!.TotalCount);
        Assert.Equal(2, pageOne.Value.Items.Count);
        Assert.Single(pageTwo.Value!.Items);
    }

    [Fact]
    public async Task Search_returns_verified_businesses_sorted_by_distance()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();

        BusinessTestSeed.SeedVerifiedBusiness(db, "lekki-cuts", "Lekki Cuts", "barbers", "Lekki", 6.4474, 3.4700);
        BusinessTestSeed.SeedVerifiedBusiness(db, "vi-salon", "VI Salon", "hair-salons", "Victoria Island", 6.4281, 3.4219);
        BusinessTestSeed.SeedDraftBusiness(db, "draft-shop", "Draft Shop", "barbers", "Lekki", 6.4474, 3.4700);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
        var result = await service.SearchAsync(6.4474, 3.4700, null, null, null, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.Equal("lekki-cuts", result.Value.Items[0].Slug);
        Assert.True(result.Value.Items[0].DistanceKm < result.Value.Items[1].DistanceKm);
    }

    [Fact]
    public async Task Search_filters_by_category()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();

        BusinessTestSeed.SeedVerifiedBusiness(db, "lekki-cuts", "Lekki Cuts", "barbers", "Lekki", 6.4474, 3.4700);
        BusinessTestSeed.SeedVerifiedBusiness(db, "vi-salon", "VI Salon", "hair-salons", "Victoria Island", 6.4281, 3.4219);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
        var result = await service.SearchAsync(6.4474, 3.4700, "barbers", null, null, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal("barbers", result.Value.Items[0].CategorySlug);
    }

    [Fact]
    public async Task Search_filters_by_market()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();

        BusinessTestSeed.SeedVerifiedBusiness(db, "lekki-cuts", "Lekki Cuts", "barbers", "Lekki", 6.4474, 3.4700, "lagos");
        BusinessTestSeed.SeedVerifiedBusiness(db, "ottawa-cuts", "Ottawa Cuts", "barbers", "Centretown", 6.4474, 3.4700, "ottawa");
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
        var result = await service.SearchAsync(6.4474, 3.4700, null, "lagos", null, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal("lagos", result.Value.Items[0].MarketId);
    }

    [Fact]
    public async Task Search_filters_by_query()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();

        BusinessTestSeed.SeedVerifiedBusiness(db, "lekki-cuts", "Lekki Cuts", "barbers", "Lekki", 6.4474, 3.4700);
        BusinessTestSeed.SeedVerifiedBusiness(db, "vi-salon", "VI Salon", "hair-salons", "Victoria Island", 6.4281, 3.4219);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
        var result = await service.SearchAsync(6.4474, 3.4700, null, null, "lekki", 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal("lekki-cuts", result.Value.Items[0].Slug);
    }

    [Fact]
    public async Task Search_filters_by_minimum_rating()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();

        var highRatedTenantId = BusinessTestSeed.SeedVerifiedBusiness(
            db, "top-cuts", "Top Cuts", "barbers", "Lekki", 6.4474, 3.4700);
        var lowRatedTenantId = BusinessTestSeed.SeedVerifiedBusiness(
            db, "budget-cuts", "Budget Cuts", "barbers", "Lekki", 6.4474, 3.4700);

        AddReview(db, highRatedTenantId, rating: 5);
        AddReview(db, lowRatedTenantId, rating: 2);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
        var result = await service.SearchAsync(
            6.4474, 3.4700, null, null, null, 1, 20, DiscoverySort.Distance, minRating: 4);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal("top-cuts", result.Value.Items[0].Slug);
    }

    private static void AddReview(AdeniDbContext db, Guid tenantId, byte rating)
    {
        db.Reviews.Add(new Review
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            BookingId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            Rating = rating,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    [Fact]
    public async Task GetPublicProfileBySlug_masks_phone_and_uses_cache()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

        BusinessTestSeed.SeedVerifiedBusiness(db, "lekki-cuts", "Lekki Cuts", "barbers", "Lekki", 6.4474, 3.4700);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
        var first = await service.GetPublicProfileBySlugAsync("lekki-cuts");
        var second = await service.GetPublicProfileBySlugAsync("lekki-cuts");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Contains("[REDACTED]", first.Value!.PhoneMasked);
        Assert.NotNull(await cache.GetAsync<Application.Discovery.PublicBusinessProfile>(
            CacheKeys.LocationProfile("lekki-cuts")));
    }

    [Fact]
    public async Task GetPublicProfileBySlug_returns_not_found_for_unverified_business()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();

        BusinessTestSeed.SeedDraftBusiness(db, "draft-shop", "Draft Shop", "barbers", "Lekki", 6.4474, 3.4700);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
        var result = await service.GetPublicProfileBySlugAsync("draft-shop");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ICacheService, DistributedCacheService>();
        services.AddSingleton<MarketCatalogState>();
        services.AddSingleton<IMarketCatalog, SyncMarketCatalog>();
        services.AddScoped<TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddSingleton<IFileStorage, FakeFileStorage>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<DiscoveryService>();
        var provider = services.BuildServiceProvider();
        var catalogState = provider.GetRequiredService<MarketCatalogState>();
        catalogState.Update(
        [
            new MarketDefinition("lagos", "Lagos", "NG", "NGN", "Africa/Lagos", new MarketLocation(6.5244, 3.3792), ["en"], true, null),
            new MarketDefinition("ottawa", "Ottawa", "CA", "CAD", "America/Toronto", new MarketLocation(45.4215, -75.6972), ["en", "fr"], false, null),
        ]);
        return provider;
    }
}
