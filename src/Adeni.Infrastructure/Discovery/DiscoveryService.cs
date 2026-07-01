namespace Adeni.Infrastructure.Discovery;

using Adeni.Application.Caching;
using Adeni.Application.Discovery;
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

        var effectivePageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        var normalizedCategory = string.IsNullOrWhiteSpace(categorySlug)
            ? null
            : categorySlug.Trim().ToLowerInvariant();

        var cacheKey = CacheKeys.Discovery(latitude, longitude, normalizedCategory, page);

        return SearchCachedAsync(
            cacheKey,
            latitude,
            longitude,
            normalizedCategory,
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
        var tenantId = await dbContext.BusinessProfiles
            .AsNoTracking()
            .Where(p => p.Slug == normalizedSlug)
            .Join(
                dbContext.Tenants.Where(t => t.Status == TenantStatus.Verified),
                profile => profile.TenantId,
                tenant => tenant.Id,
                (profile, _) => profile.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantId == Guid.Empty)
        {
            return Result.Failure<PublicBusinessProfile>(Error.NotFound("Business"));
        }

        var cacheKey = CacheKeys.TenantProfile(tenantId);
        var cached = await cache.GetAsync<PublicBusinessProfile>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return Result.Success(cached);
        }

        var loaded = await LoadPublicProfileAsync(tenantId, normalizedSlug, cancellationToken);
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
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await cache.GetOrCreateAsync(
            cacheKey,
            CacheTtl.Discovery,
            async ct =>
            {
                var items = await LoadDiscoveryItemsAsync(latitude, longitude, categorySlug, ct);
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
        CancellationToken cancellationToken)
    {
        var query = dbContext.BusinessProfiles
            .AsNoTracking()
            .Where(p => p.Latitude != null && p.Longitude != null)
            .Join(
                dbContext.Tenants.Where(t => t.Status == TenantStatus.Verified),
                profile => profile.TenantId,
                tenant => tenant.Id,
                (profile, tenant) => new { profile, tenant });

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            query = query.Where(x => x.profile.CategorySlug == categorySlug);
        }

        var businesses = await query.ToListAsync(cancellationToken);

        return businesses
            .Select(x => new DiscoveryBusinessItem(
                x.tenant.Id,
                x.tenant.Name,
                x.profile.Slug,
                x.profile.CategorySlug,
                x.profile.Area,
                GeoDistance.HaversineKm(latitude, longitude, x.profile.Latitude!.Value, x.profile.Longitude!.Value),
                x.profile.Latitude!.Value,
                x.profile.Longitude!.Value))
            .OrderBy(x => x.DistanceKm)
            .ToList();
    }

    private async Task<PublicBusinessProfile?> LoadPublicProfileAsync(
        Guid tenantId,
        string slug,
        CancellationToken cancellationToken)
    {
        var business = await dbContext.BusinessProfiles
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Slug == slug)
            .Join(
                dbContext.Tenants.Where(t => t.Status == TenantStatus.Verified),
                profile => profile.TenantId,
                tenant => tenant.Id,
                (profile, tenant) => new { profile, tenant })
            .FirstOrDefaultAsync(cancellationToken);

        if (business is null)
        {
            return null;
        }

        return new PublicBusinessProfile(
            business.tenant.Id,
            business.tenant.Name,
            business.profile.Slug,
            business.profile.CategorySlug,
            business.profile.Area,
            business.profile.AddressLine,
            business.profile.Description,
            PiiMasker.MaskPhone(business.profile.Phone),
            business.profile.Latitude,
            business.profile.Longitude);
    }
}
