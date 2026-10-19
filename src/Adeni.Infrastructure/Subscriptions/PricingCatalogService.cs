namespace Adeni.Infrastructure.Subscriptions;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Subscriptions;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class PricingCatalogService(AdeniDbContext dbContext) : IPricingCatalogService
{
    private readonly object _cacheLock = new();
    private CachedCatalog? _cache;

    public async Task EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.PricingCatalogVersions
            .AsNoTracking()
            .AnyAsync(v => v.Code == PricingCatalogSeedData.LaunchVersionCode, cancellationToken);

        if (exists)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var version = PricingCatalogSeedData.CreateLaunchVersion(now);
        dbContext.PricingCatalogVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RefreshCacheAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);

        var active = await dbContext.PricingCatalogVersions
            .AsNoTracking()
            .Include(v => v.Entries)
            .Where(v => v.Status == PricingCatalogVersionStatus.Active)
            .OrderByDescending(v => v.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (active is null)
        {
            lock (_cacheLock)
            {
                _cache = null;
            }

            return;
        }

        var snapshot = new CachedCatalog(
            active.Code,
            active.Entries.ToDictionary(
                e => Key(e.CountryCode, e.SubscriptionSku),
                e => e,
                StringComparer.OrdinalIgnoreCase));

        lock (_cacheLock)
        {
            _cache = snapshot;
        }
    }

    public string GetActiveVersionCode()
    {
        lock (_cacheLock)
        {
            return _cache?.VersionCode ?? PricingCatalogMetadata.ActiveVersionCode;
        }
    }

    public decimal? GetMonthlyMajor(string? countryCode, SubscriptionSku sku)
    {
        var key = NormalizeCountry(countryCode);
        lock (_cacheLock)
        {
            if (_cache?.Entries.TryGetValue(Key(key, sku), out var entry) == true)
            {
                return entry.MonthlyAmountMajor;
            }
        }

        return SubscriptionCatalogPrices.GetMonthlyMajor(key, sku);
    }

    public async Task<Result<PublicCountryPricingDto>> GetPublicCountryPricingAsync(
        string? countryCode,
        CancellationToken cancellationToken = default)
    {
        await RefreshCacheAsync(cancellationToken);

        var country = NormalizeCountry(countryCode);

        var skus = Enum.GetValues<SubscriptionSku>()
            .Select(sku => new PublicSkuPriceDto(SkuToApi(sku), GetMonthlyMajor(country, sku)))
            .ToList();

        var currency = SubscriptionCatalogPrices.CurrencyForCountry(country);
        var selfServe = true;
        var platformFee = 0m;

        lock (_cacheLock)
        {
            var sample = _cache?.Entries.Values.FirstOrDefault(e =>
                string.Equals(e.CountryCode, country, StringComparison.OrdinalIgnoreCase));
            if (sample is not null)
            {
                currency = sample.CurrencyCode;
                selfServe = sample.IsSelfServeEnabled;
                platformFee = sample.PlatformFeePercent;
            }
        }

        var hasCountryRow = false;
        lock (_cacheLock)
        {
            hasCountryRow = _cache?.Entries.Values.Any(e =>
                string.Equals(e.CountryCode, country, StringComparison.OrdinalIgnoreCase)) == true;
        }

        if (!hasCountryRow && skus.All(s => s.MonthlyAmountMajor is null))
        {
            return Result.Failure<PublicCountryPricingDto>(
                new Error("pricing.country_not_found", "Country pricing is not available."));
        }

        return Result.Success(new PublicCountryPricingDto(
            country,
            currency,
            GetActiveVersionCode(),
            selfServe,
            platformFee,
            skus));
    }

    public async Task<Result<IReadOnlyList<PricingCatalogVersionSummaryDto>>> ListVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);

        await RefreshCacheAsync(cancellationToken);

        var activeVersionId = await dbContext.PricingCatalogVersions
            .AsNoTracking()
            .Where(v => v.Status == PricingCatalogVersionStatus.Active)
            .OrderByDescending(v => v.EffectiveFrom)
            .Select(v => v.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var versions = await dbContext.PricingCatalogVersions
            .AsNoTracking()
            .OrderByDescending(v => v.EffectiveFrom)
            .Select(v => new PricingCatalogVersionSummaryDto(
                v.Id,
                v.Code,
                v.EffectiveFrom,
                v.EffectiveTo,
                v.Status.ToString().ToLowerInvariant(),
                v.Id == activeVersionId && v.Status == PricingCatalogVersionStatus.Active))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<PricingCatalogVersionSummaryDto>>(versions);
    }

    public async Task<Result<PricingCatalogVersionDetailDto>> GetVersionAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeededAsync(cancellationToken);
        await RefreshCacheAsync(cancellationToken);

        var normalized = code.Trim();
        var version = await dbContext.PricingCatalogVersions
            .AsNoTracking()
            .Include(v => v.Entries)
            .FirstOrDefaultAsync(v => v.Code == normalized, cancellationToken);

        if (version is null)
        {
            return Result.Failure<PricingCatalogVersionDetailDto>(
                Error.NotFound("Pricing catalog version"));
        }

        var activeCode = GetActiveVersionCode();
        var isActive = version.Status == PricingCatalogVersionStatus.Active
            && string.Equals(version.Code, activeCode, StringComparison.OrdinalIgnoreCase);

        var entries = version.Entries
            .OrderBy(e => e.CountryCode)
            .ThenBy(e => e.SubscriptionSku)
            .Select(MapEntry)
            .ToList();

        IReadOnlyList<PricingCatalogEntryDiffDto>? diff = null;
        if (!isActive)
        {
            diff = BuildDiff(entries);
        }

        return Result.Success(new PricingCatalogVersionDetailDto(
            version.Id,
            version.Code,
            version.EffectiveFrom,
            version.EffectiveTo,
            version.Status.ToString().ToLowerInvariant(),
            isActive,
            entries,
            diff));
    }

    private IReadOnlyList<PricingCatalogEntryDiffDto> BuildDiff(IReadOnlyList<PricingCatalogEntryDto> entries)
    {
        var diffs = new List<PricingCatalogEntryDiffDto>();
        foreach (var entry in entries)
        {
            if (!TryParseSku(entry.SubscriptionSku, out var skuEnum))
            {
                continue;
            }

            var activeAmount = GetMonthlyMajor(entry.CountryCode, skuEnum);
            if (activeAmount != entry.MonthlyAmountMajor)
            {
                diffs.Add(new PricingCatalogEntryDiffDto(
                    entry.CountryCode,
                    entry.SubscriptionSku,
                    activeAmount,
                    entry.MonthlyAmountMajor));
            }
        }

        return diffs;
    }

    private static PricingCatalogEntryDto MapEntry(PricingCatalogEntry e) =>
        new(
            e.CountryCode,
            e.CurrencyCode,
            SkuToApi(e.SubscriptionSku),
            e.MonthlyAmountMajor,
            e.PlatformFeePercent,
            e.IsSelfServeEnabled);

    private static string SkuToApi(SubscriptionSku sku) =>
        sku.ToString().ToLowerInvariant();

    private static bool TryParseSku(string sku, out SubscriptionSku parsed) =>
        Enum.TryParse(sku, ignoreCase: true, out parsed);

    private static string NormalizeCountry(string? countryCode) =>
        string.IsNullOrWhiteSpace(countryCode)
            ? SubscriptionCatalogPrices.DefaultCountryCode
            : countryCode.Trim().ToUpperInvariant();

    private static string Key(string countryCode, SubscriptionSku sku) =>
        $"{countryCode}:{(int)sku}";

    private sealed record CachedCatalog(
        string VersionCode,
        IReadOnlyDictionary<string, PricingCatalogEntry> Entries);
}
