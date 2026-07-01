namespace Adeni.Infrastructure.Tests.Discovery;

using Adeni.Application.Caching;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Discovery;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

public sealed class DiscoveryServiceTests
{
    [Fact]
    public async Task Search_returns_verified_businesses_sorted_by_distance()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();

        SeedVerifiedBusiness(db, "lekki-cuts", "Lekki Cuts", "barbers", "Lekki", 6.4474, 3.4700);
        SeedVerifiedBusiness(db, "vi-salon", "VI Salon", "hair-salons", "Victoria Island", 6.4281, 3.4219);
        SeedDraftBusiness(db, "draft-shop", "Draft Shop", "barbers", "Lekki", 6.4474, 3.4700);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
        var result = await service.SearchAsync(6.4474, 3.4700, null, 1, 20);

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

        SeedVerifiedBusiness(db, "lekki-cuts", "Lekki Cuts", "barbers", "Lekki", 6.4474, 3.4700);
        SeedVerifiedBusiness(db, "vi-salon", "VI Salon", "hair-salons", "Victoria Island", 6.4281, 3.4219);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
        var result = await service.SearchAsync(6.4474, 3.4700, "barbers", 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal("barbers", result.Value.Items[0].CategorySlug);
    }

    [Fact]
    public async Task GetPublicProfileBySlug_masks_phone_and_uses_cache()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

        var tenantId = SeedVerifiedBusiness(db, "lekki-cuts", "Lekki Cuts", "barbers", "Lekki", 6.4474, 3.4700);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
        var first = await service.GetPublicProfileBySlugAsync("lekki-cuts");
        var second = await service.GetPublicProfileBySlugAsync("lekki-cuts");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Contains("[REDACTED]", first.Value!.PhoneMasked);
        Assert.NotNull(await cache.GetAsync<Application.Discovery.PublicBusinessProfile>(CacheKeys.TenantProfile(tenantId)));
    }

    [Fact]
    public async Task GetPublicProfileBySlug_returns_not_found_for_unverified_business()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();

        SeedDraftBusiness(db, "draft-shop", "Draft Shop", "barbers", "Lekki", 6.4474, 3.4700);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
        var result = await service.GetPublicProfileBySlugAsync("draft-shop");

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error.Code);
    }

    private static Guid SeedVerifiedBusiness(
        AdeniDbContext db,
        string slug,
        string name,
        string category,
        string area,
        double lat,
        double lng)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = name,
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            Slug = slug,
            CategorySlug = category,
            Area = area,
            AddressLine = "Test address",
            Phone = "+2348012345678",
            Description = "Test",
            Latitude = lat,
            Longitude = lng,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        return tenantId;
    }

    private static void SeedDraftBusiness(
        AdeniDbContext db,
        string slug,
        string name,
        string category,
        string area,
        double lat,
        double lng)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = name,
            Status = TenantStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            Slug = slug,
            CategorySlug = category,
            Area = area,
            AddressLine = "Test address",
            Phone = "+2348012345678",
            Latitude = lat,
            Longitude = lng,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ICacheService, DistributedCacheService>();
        services.AddScoped<TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<DiscoveryService>();
        return services.BuildServiceProvider();
    }
}
