namespace Adeni.Infrastructure.Discovery;

using Adeni.Application.Caching;
using Adeni.Application.Discovery;
using Adeni.Application.Markets;
using Adeni.Application.Security;
using Adeni.Application.Storage;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class DiscoveryService(
    AdeniDbContext dbContext,
    ICacheService cache,
    IFileStorage fileStorage,
    Application.Reviews.IReviewService reviewService,
    IMarketCatalog marketCatalog) : IDiscoveryService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 50;

    public Task<Result<DiscoveryResult>> SearchAsync(
        double latitude,
        double longitude,
        string? categorySlug,
        string? marketId,
        string? query,
        int page,
        int pageSize,
        DiscoverySort sort = DiscoverySort.Distance,
        int? minRating = null,
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

        if (minRating is < 1 or > 5)
        {
            return Task.FromResult(Result.Failure<DiscoveryResult>(Error.Validation("Minimum rating must be between 1 and 5.")));
        }

        string? normalizedMarket = null;
        if (!string.IsNullOrWhiteSpace(marketId))
        {
            if (!marketCatalog.IsValid(marketId))
            {
                return Task.FromResult(Result.Failure<DiscoveryResult>(Error.Validation("Market is not valid.")));
            }

            normalizedMarket = marketCatalog.Normalize(marketId);
        }

        var effectivePageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        var normalizedCategory = string.IsNullOrWhiteSpace(categorySlug)
            ? null
            : categorySlug.Trim().ToLowerInvariant();
        var normalizedQuery = string.IsNullOrWhiteSpace(query)
            ? null
            : query.Trim().ToLowerInvariant();
        var sortKey = sort == DiscoverySort.Featured ? "featured" : "distance";

        var cacheKey = CacheKeys.Discovery(
            latitude,
            longitude,
            normalizedCategory,
            normalizedMarket,
            page,
            effectivePageSize,
            normalizedQuery,
            sortKey,
            minRating);

        return SearchCachedAsync(
            cacheKey,
            latitude,
            longitude,
            normalizedCategory,
            normalizedMarket,
            normalizedQuery,
            page,
            effectivePageSize,
            sort,
            minRating,
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
        string? searchQuery,
        int page,
        int pageSize,
        DiscoverySort sort,
        int? minRating,
        CancellationToken cancellationToken)
    {
        var result = await cache.GetOrCreateAsync(
            cacheKey,
            CacheTtl.Discovery,
            ct => LoadDiscoveryPageAsync(
                latitude,
                longitude,
                categorySlug,
                marketId,
                searchQuery,
                page,
                pageSize,
                sort,
                minRating,
                ct),
            cancellationToken);

        return Result.Success(result);
    }

    private async Task<DiscoveryResult> LoadDiscoveryPageAsync(
        double latitude,
        double longitude,
        string? categorySlug,
        string? marketId,
        string? searchQuery,
        int page,
        int pageSize,
        DiscoverySort sort,
        int? minRating,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational())
        {
            return await LoadDiscoveryPageRelationalAsync(
                latitude,
                longitude,
                categorySlug,
                marketId,
                searchQuery,
                page,
                pageSize,
                sort,
                minRating,
                cancellationToken);
        }

        return await LoadDiscoveryPageInMemoryAsync(
            latitude,
            longitude,
            categorySlug,
            marketId,
            searchQuery,
            page,
            pageSize,
            sort,
            minRating,
            cancellationToken);
    }

    private async Task<DiscoveryResult> LoadDiscoveryPageRelationalAsync(
        double latitude,
        double longitude,
        string? categorySlug,
        string? marketId,
        string? searchQuery,
        int page,
        int pageSize,
        DiscoverySort sort,
        int? minRating,
        CancellationToken cancellationToken)
    {
        var verifiedStatus = (int)TenantStatus.Verified;
        var likeQuery = searchQuery is null ? null : $"%{searchQuery}%";
        var offset = (page - 1) * pageSize;

        var totalCount = await dbContext.Database
            .SqlQuery<int>($"""
                WITH rating_summary AS (
                    SELECT
                        r."TenantId" AS tenant_id,
                        ROUND(AVG(r."Rating")::numeric, 1)::float AS rating_avg
                    FROM booking.reviews r
                    WHERE NOT r."IsHidden"
                    GROUP BY r."TenantId"
                )
                SELECT COUNT(*)::int AS "Value"
                FROM tenancy.business_locations bl
                INNER JOIN tenancy.tenants t ON t."Id" = bl."TenantId"
                INNER JOIN tenancy.business_profiles bp ON bp."TenantId" = t."Id"
                LEFT JOIN rating_summary ON rating_summary.tenant_id = t."Id"
                WHERE bl."IsActive" = TRUE
                  AND bl."Latitude" IS NOT NULL
                  AND bl."Longitude" IS NOT NULL
                  AND t."Status" = {verifiedStatus}
                  AND ({categorySlug}::text IS NULL OR LOWER(bp."CategorySlug") = {categorySlug})
                  AND ({marketId}::text IS NULL OR LOWER(bl."MarketId") = {marketId})
                  AND ({minRating}::int IS NULL OR COALESCE(rating_summary.rating_avg, 0) >= {minRating}::int)
                  AND (
                    {likeQuery}::text IS NULL OR (
                      LOWER(t."Name") LIKE {likeQuery}
                      OR LOWER(bl."Name") LIKE {likeQuery}
                      OR LOWER(bl."Area") LIKE {likeQuery}
                      OR LOWER(bp."CategorySlug") LIKE {likeQuery}
                      OR LOWER(bp."Description") LIKE {likeQuery}
                    )
                  )
                """)
            .SingleAsync(cancellationToken);

        List<DiscoverySearchRow> rows = sort == DiscoverySort.Featured
            ? await dbContext.Database.SqlQuery<DiscoverySearchRow>($"""
                WITH rating_summary AS (
                    SELECT
                        r."TenantId" AS tenant_id,
                        ROUND(AVG(r."Rating")::numeric, 1)::float AS rating_avg,
                        COUNT(*)::int AS review_count
                    FROM booking.reviews r
                    WHERE NOT r."IsHidden"
                    GROUP BY r."TenantId"
                ),
                candidates AS (
                    SELECT
                        bl."Id" AS "LocationId",
                        bl."TenantId" AS "TenantId",
                        t."Name" AS "Name",
                        bl."Name" AS "LocationName",
                        bl."Slug" AS "Slug",
                        bp."CategorySlug" AS "CategorySlug",
                        bl."Area" AS "Area",
                        bl."MarketId" AS "MarketId",
                        bp."CoverImageKey" AS "CoverImageKey",
                        bl."Latitude" AS "Latitude",
                        bl."Longitude" AS "Longitude",
                        (6371.0 * 2 * ASIN(SQRT(
                            POWER(SIN(RADIANS(bl."Latitude" - {latitude}) / 2.0), 2) +
                            COS(RADIANS({latitude})) * COS(RADIANS(bl."Latitude")) *
                            POWER(SIN(RADIANS(bl."Longitude" - {longitude}) / 2.0), 2)
                        ))) AS distance_km,
                        rating_summary.rating_avg AS "RatingAvg",
                        COALESCE(rating_summary.review_count, 0) AS "ReviewCount"
                    FROM tenancy.business_locations bl
                    INNER JOIN tenancy.tenants t ON t."Id" = bl."TenantId"
                    INNER JOIN tenancy.business_profiles bp ON bp."TenantId" = t."Id"
                    LEFT JOIN rating_summary ON rating_summary.tenant_id = t."Id"
                    WHERE bl."IsActive" = TRUE
                      AND bl."Latitude" IS NOT NULL
                      AND bl."Longitude" IS NOT NULL
                      AND t."Status" = {verifiedStatus}
                      AND ({categorySlug}::text IS NULL OR LOWER(bp."CategorySlug") = {categorySlug})
                      AND ({marketId}::text IS NULL OR LOWER(bl."MarketId") = {marketId})
                      AND ({minRating}::int IS NULL OR COALESCE(rating_summary.rating_avg, 0) >= {minRating}::int)
                      AND (
                        {likeQuery}::text IS NULL OR (
                          LOWER(t."Name") LIKE {likeQuery}
                          OR LOWER(bl."Name") LIKE {likeQuery}
                          OR LOWER(bl."Area") LIKE {likeQuery}
                          OR LOWER(bp."CategorySlug") LIKE {likeQuery}
                          OR LOWER(bp."Description") LIKE {likeQuery}
                        )
                      )
                )
                SELECT
                    "LocationId",
                    "TenantId",
                    "Name",
                    "LocationName",
                    "Slug",
                    "CategorySlug",
                    "Area",
                    "MarketId",
                    "CoverImageKey",
                    ROUND(distance_km::numeric, 2)::float AS "DistanceKm",
                    "Latitude",
                    "Longitude",
                    "RatingAvg",
                    "ReviewCount"
                FROM candidates
                ORDER BY
                  COALESCE("RatingAvg", 0) DESC,
                  "ReviewCount" DESC,
                  "DistanceKm" ASC
                OFFSET {offset} LIMIT {pageSize}
                """).ToListAsync(cancellationToken)
            : await dbContext.Database.SqlQuery<DiscoverySearchRow>($"""
                WITH rating_summary AS (
                    SELECT
                        r."TenantId" AS tenant_id,
                        ROUND(AVG(r."Rating")::numeric, 1)::float AS rating_avg
                    FROM booking.reviews r
                    WHERE NOT r."IsHidden"
                    GROUP BY r."TenantId"
                ),
                candidates AS (
                    SELECT
                        bl."Id" AS "LocationId",
                        bl."TenantId" AS "TenantId",
                        t."Name" AS "Name",
                        bl."Name" AS "LocationName",
                        bl."Slug" AS "Slug",
                        bp."CategorySlug" AS "CategorySlug",
                        bl."Area" AS "Area",
                        bl."MarketId" AS "MarketId",
                        bp."CoverImageKey" AS "CoverImageKey",
                        bl."Latitude" AS "Latitude",
                        bl."Longitude" AS "Longitude",
                        (6371.0 * 2 * ASIN(SQRT(
                            POWER(SIN(RADIANS(bl."Latitude" - {latitude}) / 2.0), 2) +
                            COS(RADIANS({latitude})) * COS(RADIANS(bl."Latitude")) *
                            POWER(SIN(RADIANS(bl."Longitude" - {longitude}) / 2.0), 2)
                        ))) AS distance_km
                    FROM tenancy.business_locations bl
                    INNER JOIN tenancy.tenants t ON t."Id" = bl."TenantId"
                    INNER JOIN tenancy.business_profiles bp ON bp."TenantId" = t."Id"
                    LEFT JOIN rating_summary ON rating_summary.tenant_id = t."Id"
                    WHERE bl."IsActive" = TRUE
                      AND bl."Latitude" IS NOT NULL
                      AND bl."Longitude" IS NOT NULL
                      AND t."Status" = {verifiedStatus}
                      AND ({categorySlug}::text IS NULL OR LOWER(bp."CategorySlug") = {categorySlug})
                      AND ({marketId}::text IS NULL OR LOWER(bl."MarketId") = {marketId})
                      AND ({minRating}::int IS NULL OR COALESCE(rating_summary.rating_avg, 0) >= {minRating}::int)
                      AND (
                        {likeQuery}::text IS NULL OR (
                          LOWER(t."Name") LIKE {likeQuery}
                          OR LOWER(bl."Name") LIKE {likeQuery}
                          OR LOWER(bl."Area") LIKE {likeQuery}
                          OR LOWER(bp."CategorySlug") LIKE {likeQuery}
                          OR LOWER(bp."Description") LIKE {likeQuery}
                        )
                      )
                )
                SELECT
                    "LocationId",
                    "TenantId",
                    "Name",
                    "LocationName",
                    "Slug",
                    "CategorySlug",
                    "Area",
                    "MarketId",
                    "CoverImageKey",
                    ROUND(distance_km::numeric, 2)::float AS "DistanceKm",
                    "Latitude",
                    "Longitude",
                    NULL::float AS "RatingAvg",
                    0 AS "ReviewCount"
                FROM candidates
                ORDER BY "DistanceKm" ASC
                OFFSET {offset} LIMIT {pageSize}
                """).ToListAsync(cancellationToken);

        if (rows.Count > 0 && sort == DiscoverySort.Distance)
        {
            var tenantIds = rows.Select(x => x.TenantId).Distinct().ToArray();
            var ratings = await reviewService.GetRatingSummariesAsync(tenantIds, cancellationToken);
            rows = rows
                .Select(row =>
                {
                    ratings.TryGetValue(row.TenantId, out var summary);
                    row.RatingAvg = summary?.RatingAvg;
                    row.ReviewCount = summary?.ReviewCount ?? 0;
                    return row;
                })
                .ToList();
        }

        var items = new List<DiscoveryBusinessItem>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(new DiscoveryBusinessItem(
                row.LocationId,
                row.TenantId,
                row.Name,
                row.LocationName,
                row.Slug,
                row.CategorySlug,
                row.Area,
                row.MarketId,
                await ResolveCoverImageUrlAsync(row.CoverImageKey, cancellationToken),
                row.RatingAvg,
                row.ReviewCount,
                row.DistanceKm,
                row.Latitude,
                row.Longitude));
        }

        return new DiscoveryResult(items, page, pageSize, totalCount);
    }

    private async Task<DiscoveryResult> LoadDiscoveryPageInMemoryAsync(
        double latitude,
        double longitude,
        string? categorySlug,
        string? marketId,
        string? searchQuery,
        int page,
        int pageSize,
        DiscoverySort sort,
        int? minRating,
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

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            query = query.Where(x =>
                x.tenant.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)
                || x.location.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)
                || x.location.Area.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)
                || x.profile.CategorySlug.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)
                || x.profile.Description.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
        }

        var businesses = await query.ToListAsync(cancellationToken);

        var tenantIds = businesses.Select(x => x.tenant.Id).Distinct().ToArray();
        var ratings = await reviewService.GetRatingSummariesAsync(tenantIds, cancellationToken);

        var ordered = businesses
            .Select(x =>
            {
                ratings.TryGetValue(x.tenant.Id, out var summary);
                var distanceKm = GeoDistance.HaversineKm(
                    latitude,
                    longitude,
                    x.location.Latitude!.Value,
                    x.location.Longitude!.Value);

                return new
                {
                    x.location,
                    x.tenant,
                    x.profile,
                    summary,
                    distanceKm,
                };
            })
            .Where(x => minRating is null || (x.summary?.RatingAvg ?? 0) >= minRating)
            .ToList();

        var totalCount = ordered.Count;

        var pageRows = (sort == DiscoverySort.Featured
            ? ordered
                .OrderByDescending(x => x.summary?.RatingAvg ?? 0)
                .ThenByDescending(x => x.summary?.ReviewCount ?? 0)
                .ThenBy(x => x.distanceKm)
            : ordered.OrderBy(x => x.distanceKm))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var items = new List<DiscoveryBusinessItem>(pageRows.Count);
        foreach (var row in pageRows)
        {
            items.Add(new DiscoveryBusinessItem(
                row.location.Id,
                row.tenant.Id,
                row.tenant.Name,
                row.location.Name,
                row.location.Slug,
                row.profile.CategorySlug,
                row.location.Area,
                row.location.MarketId,
                await ResolveCoverImageUrlAsync(row.profile.CoverImageKey, cancellationToken),
                row.summary?.RatingAvg,
                row.summary?.ReviewCount ?? 0,
                Math.Round(row.distanceKm, 2),
                row.location.Latitude!.Value,
                row.location.Longitude!.Value));
        }

        return new DiscoveryResult(items, page, pageSize, totalCount);
    }

    private async Task<string?> ResolveCoverImageUrlAsync(
        string? coverImageKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(coverImageKey))
        {
            return null;
        }

        return await fileStorage.GetDownloadUrlAsync(coverImageKey, cancellationToken);
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

        var ratings = await reviewService.GetRatingSummariesAsync([business.tenant.Id], cancellationToken);
        ratings.TryGetValue(business.tenant.Id, out var summary);

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
            await ResolveCoverImageUrlAsync(business.profile.CoverImageKey, cancellationToken),
            summary?.RatingAvg,
            summary?.ReviewCount ?? 0,
            business.location.Latitude,
            business.location.Longitude);
    }
}
