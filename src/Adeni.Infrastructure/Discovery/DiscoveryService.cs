namespace Adeni.Infrastructure.Discovery;

using Adeni.Application.Caching;
using Adeni.Application.Discovery;
using Adeni.Application.Markets;
using Adeni.Application.Security;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class DiscoveryService(
    AdeniDbContext dbContext,
    ICacheService cache) : IDiscoveryService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    public Task<Result<DiscoveryResult>> SearchAsync(
        double latitude,
        double longitude,
        string? categorySlug,
        string? marketId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return Task.FromResult(Result.Failure<DiscoveryResult>(
                Error.Validation("Latitude must be between -90 and 90 and longitude between -180 and 180.")));
        }

        if (page < 1)
        {
            return Task.FromResult(Result.Failure<DiscoveryResult>(Error.Validation("Page must be at least 1.")));
        }

        string? normalizedMarket = null;
        if (!string.IsNullOrWhiteSpace(marketId))
        {
            if (!KnownMarketCatalog.IsValid(marketId))
            {
                return Task.FromResult(Result.Failure<DiscoveryResult>(Error.Validation("Market is not valid.")));
            }

            normalizedMarket = KnownMarketCatalog.Normalize(marketId);
        }

        var effectivePageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        var normalizedCategory = string.IsNullOrWhiteSpace(categorySlug)
            ? null
            : categorySlug.Trim().ToLowerInvariant();

        var cacheKey = CacheKeys.Discovery(latitude, longitude, normalizedCategory, normalizedMarket, page);

        return SearchCachedAsync(
            cacheKey,
            latitude,
            longitude,
            normalizedCategory,
            normalizedMarket,
            page,
            effectivePageSize,
            cancellationToken);
    }

    public async Task<Result<PublicBusinessProfile>> GetPublicProfileBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return Result.Failure<PublicBusinessProfile>(Error.Validation("Business slug is required."));
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var cacheKey = CacheKeys.LocationProfile(normalizedSlug);
        var cached = await cache.GetAsync<PublicBusinessProfile>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return Result.Success(cached);
        }

        var loaded = await LoadPublicProfileAsync(normalizedSlug, cancellationToken);
        if (loaded is null)
        {
            return Result.Failure<PublicBusinessProfile>(Error.NotFound("Business"));
        }

        await cache.SetAsync(cacheKey, loaded, CacheTtl.TenantProfile, cancellationToken);
        return Result.Success(loaded);
    }

    private async Task<Result<DiscoveryResult>> SearchCachedAsync(
        string cacheKey,
        double latitude,
        double longitude,
        string? categorySlug,
        string? marketId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await cache.GetOrCreateAsync(
            cacheKey,
            CacheTtl.Discovery,
            async ct =>
            {
                var items = await LoadDiscoveryItemsAsync(latitude, longitude, categorySlug, marketId, ct);
                var totalCount = items.Count;
                var pageItems = items
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return new DiscoveryResult(pageItems, page, pageSize, totalCount);
            },
            cancellationToken);

        return Result.Success(result);
    }

    private async Task<IReadOnlyList<DiscoveryBusinessItem>> LoadDiscoveryItemsAsync(
        double latitude,
        double longitude,
        string? categorySlug,
        string? marketId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.BusinessLocations
            .AsNoTracking()
            .Where(location => location.IsActive && location.Latitude != null && location.Longitude != null)
            .Join(
                dbContext.Tenants.Where(tenant => tenant.Status == TenantStatus.Verified),
                location => location.TenantId,
                tenant => tenant.Id,
                (location, tenant) => new { location, tenant })
            .Join(
                dbContext.BusinessProfiles.AsNoTracking(),
                x => x.tenant.Id,
                profile => profile.TenantId,
                (x, profile) => new { x.location, x.tenant, profile });

        if (!string.IsNullOrWhiteSpace(marketId))
        {
            query = query.Where(x => x.location.MarketId == marketId);
        }

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            query = query.Where(x => x.profile.CategorySlug == categorySlug);
        }

        var businesses = await query.ToListAsync(cancellationToken);

        return businesses
            .Select(x => new DiscoveryBusinessItem(
                x.location.Id,
                x.tenant.Id,
                x.tenant.Name,
                x.location.Name,
                x.location.Slug,
                x.profile.CategorySlug,
                x.location.Area,
                x.location.MarketId,
                GeoDistance.HaversineKm(
                    latitude,
                    longitude,
                    x.location.Latitude!.Value,
                    x.location.Longitude!.Value),
                x.location.Latitude!.Value,
                x.location.Longitude!.Value))
            .OrderBy(x => x.DistanceKm)
            .ToList();
    }

    private async Task<PublicBusinessProfile?> LoadPublicProfileAsync(
        string slug,
        CancellationToken cancellationToken)
    {
        var business = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(location => location.IsActive && location.Slug == slug)
            .Join(
                dbContext.Tenants.Where(tenant => tenant.Status == TenantStatus.Verified),
                location => location.TenantId,
                tenant => tenant.Id,
                (location, tenant) => new { location, tenant })
            .Join(
                dbContext.BusinessProfiles.AsNoTracking(),
                x => x.tenant.Id,
                profile => profile.TenantId,
                (x, profile) => new { x.location, x.tenant, profile })
            .FirstOrDefaultAsync(cancellationToken);

        if (business is null)
        {
            return null;
        }

        return new PublicBusinessProfile(
            business.location.Id,
            business.tenant.Id,
            business.tenant.Name,
            business.location.Name,
            business.location.Slug,
            business.profile.CategorySlug,
            business.location.Area,
            business.location.MarketId,
            business.location.AddressLine,
            business.profile.Description,
            PiiMasker.MaskPhone(business.profile.Phone),
            business.location.Latitude,
            business.location.Longitude);
    }
}
