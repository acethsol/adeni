namespace Adeni.Infrastructure.Subscriptions;

using System.Text.Json;
using Adeni.Application.Abstractions;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Subscriptions;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class PricingCatalogService(
    AdeniDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    ICorrelationContext correlationContext) : IPricingCatalogService
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
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

    public decimal GetPlatformFeePercent(string? countryCode)
    {
        var sample = GetSampleCountryEntry(countryCode);
        return sample?.PlatformFeePercent ?? 0m;
    }

    public bool GetSelfServeEnabled(string? countryCode)
    {
        var sample = GetSampleCountryEntry(countryCode);
        return sample?.IsSelfServeEnabled ?? true;
    }

    private PricingCatalogEntry? GetSampleCountryEntry(string? countryCode)
    {
        var key = NormalizeCountry(countryCode);
        lock (_cacheLock)
        {
            return _cache?.Entries.Values.FirstOrDefault(e =>
                string.Equals(e.CountryCode, key, StringComparison.OrdinalIgnoreCase));
        }
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

    public async Task<Result<PricingCatalogVersionDetailDto>> CreateDraftVersionAsync(
        CreatePricingCatalogVersionRequest request,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        if (request.EffectiveFrom == default)
        {
            return Result.Failure<PricingCatalogVersionDetailDto>(
                Error.Validation("Effective from date is required."));
        }

        var validation = ValidateVersionDates(request.EffectiveFrom, request.EffectiveTo);
        if (validation.IsFailure)
        {
            return Result.Failure<PricingCatalogVersionDetailDto>(validation.Error);
        }

        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 64)
        {
            return Result.Failure<PricingCatalogVersionDetailDto>(
                Error.Validation("Catalog version code is required (max 64 characters)."));
        }

        if (await dbContext.PricingCatalogVersions.AnyAsync(v => v.Code == code, cancellationToken))
        {
            return Result.Failure<PricingCatalogVersionDetailDto>(
                Error.Conflict("A pricing catalog version with this code already exists."));
        }

        await EnsureSeededAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var version = new PricingCatalogVersion
        {
            Id = Guid.NewGuid(),
            Code = code,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Status = PricingCatalogVersionStatus.Draft,
            CreatedAt = now,
            Entries = [],
        };

        if (request.CloneFromActive)
        {
            var active = await dbContext.PricingCatalogVersions
                .AsNoTracking()
                .Include(v => v.Entries)
                .Where(v => v.Status == PricingCatalogVersionStatus.Active)
                .OrderByDescending(v => v.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken);

            if (active is not null)
            {
                foreach (var source in active.Entries)
                {
                    version.Entries.Add(CloneEntry(version.Id, source));
                }
            }
        }

        dbContext.PricingCatalogVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogWriter.WriteAsync(new AuditEntry(
            Guid.NewGuid(),
            adminId,
            AuditActions.PricingCatalogVersionCreated,
            "pricing_catalog_version",
            version.Code,
            correlationContext.CorrelationId,
            now,
            JsonSerializer.Serialize(new { version.Code, request.CloneFromActive }, AuditJsonOptions)),
            cancellationToken);

        return await GetVersionAsync(version.Code, cancellationToken);
    }

    public async Task<Result<PricingCatalogVersionDetailDto>> UpsertEntriesAsync(
        string code,
        UpsertPricingCatalogEntriesRequest request,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        if (request.Entries.Count == 0)
        {
            return Result.Failure<PricingCatalogVersionDetailDto>(
                Error.Validation("At least one catalog entry is required."));
        }

        var normalized = code.Trim();
        var version = await dbContext.PricingCatalogVersions
            .Include(v => v.Entries)
            .FirstOrDefaultAsync(v => v.Code == normalized, cancellationToken);

        if (version is null)
        {
            return Result.Failure<PricingCatalogVersionDetailDto>(
                Error.NotFound("Pricing catalog version"));
        }

        if (version.Status != PricingCatalogVersionStatus.Draft)
        {
            return Result.Failure<PricingCatalogVersionDetailDto>(
                Error.Conflict("Only draft catalog versions can be edited."));
        }

        var parsedEntries = new List<(PricingCatalogEntryInputDto Input, SubscriptionSku Sku)>();
        foreach (var input in request.Entries)
        {
            var entryValidation = ValidateEntryInput(input, out var sku);
            if (entryValidation.IsFailure)
            {
                return Result.Failure<PricingCatalogVersionDetailDto>(entryValidation.Error);
            }

            parsedEntries.Add((input, sku));
        }

        var duplicateKeys = parsedEntries
            .GroupBy(e => Key(NormalizeCountry(e.Input.CountryCode), e.Sku))
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateKeys.Count > 0)
        {
            return Result.Failure<PricingCatalogVersionDetailDto>(
                Error.Validation("Duplicate country and SKU rows in the same request."));
        }

        foreach (var (input, sku) in parsedEntries)
        {
            var country = NormalizeCountry(input.CountryCode);
            var currency = input.CurrencyCode.Trim().ToUpperInvariant();
            var existing = version.Entries.FirstOrDefault(e =>
                string.Equals(e.CountryCode, country, StringComparison.OrdinalIgnoreCase)
                && e.SubscriptionSku == sku);

            if (existing is null)
            {
                version.Entries.Add(new PricingCatalogEntry
                {
                    Id = Guid.NewGuid(),
                    PricingCatalogVersionId = version.Id,
                    CountryCode = country,
                    CurrencyCode = currency,
                    SubscriptionSku = sku,
                    MonthlyAmountMajor = input.MonthlyAmountMajor,
                    PlatformFeePercent = input.PlatformFeePercent,
                    IsSelfServeEnabled = input.IsSelfServeEnabled,
                });
            }
            else
            {
                existing.CurrencyCode = currency;
                existing.MonthlyAmountMajor = input.MonthlyAmountMajor;
                existing.PlatformFeePercent = input.PlatformFeePercent;
                existing.IsSelfServeEnabled = input.IsSelfServeEnabled;
            }
        }

        var now = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLogWriter.WriteAsync(new AuditEntry(
            Guid.NewGuid(),
            adminId,
            AuditActions.PricingCatalogEntriesUpdated,
            "pricing_catalog_version",
            version.Code,
            correlationContext.CorrelationId,
            now,
            JsonSerializer.Serialize(new { version.Code, entryCount = parsedEntries.Count }, AuditJsonOptions)),
            cancellationToken);

        return await GetVersionAsync(version.Code, cancellationToken);
    }

    public async Task<Result<PricingCatalogVersionDetailDto>> ActivateVersionAsync(
        string code,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim();
        var version = await dbContext.PricingCatalogVersions
            .FirstOrDefaultAsync(v => v.Code == normalized, cancellationToken);

        if (version is null)
        {
            return Result.Failure<PricingCatalogVersionDetailDto>(
                Error.NotFound("Pricing catalog version"));
        }

        if (version.Status != PricingCatalogVersionStatus.Draft)
        {
            return Result.Failure<PricingCatalogVersionDetailDto>(
                Error.Conflict("Only draft catalog versions can be activated."));
        }

        var dateValidation = ValidateVersionDates(version.EffectiveFrom, version.EffectiveTo);
        if (dateValidation.IsFailure)
        {
            return Result.Failure<PricingCatalogVersionDetailDto>(dateValidation.Error);
        }

        var hasEntries = await dbContext.PricingCatalogEntries
            .AnyAsync(e => e.PricingCatalogVersionId == version.Id, cancellationToken);
        if (!hasEntries)
        {
            return Result.Failure<PricingCatalogVersionDetailDto>(
                Error.Validation("Cannot activate a catalog version with no entries."));
        }

        var now = DateTimeOffset.UtcNow;
        var priorActive = await dbContext.PricingCatalogVersions
            .Where(v => v.Status == PricingCatalogVersionStatus.Active && v.Id != version.Id)
            .ToListAsync(cancellationToken);

        foreach (var prior in priorActive)
        {
            prior.Status = PricingCatalogVersionStatus.Retired;
            prior.EffectiveTo ??= version.EffectiveFrom <= now ? version.EffectiveFrom : now;
        }

        version.Status = PricingCatalogVersionStatus.Active;
        await dbContext.SaveChangesAsync(cancellationToken);
        await RefreshCacheAsync(cancellationToken);

        await auditLogWriter.WriteAsync(new AuditEntry(
            Guid.NewGuid(),
            adminId,
            AuditActions.PricingCatalogVersionActivated,
            "pricing_catalog_version",
            version.Code,
            correlationContext.CorrelationId,
            now,
            JsonSerializer.Serialize(
                new { version.Code, retiredVersionCodes = priorActive.Select(p => p.Code).ToArray() },
                AuditJsonOptions)),
            cancellationToken);

        return await GetVersionAsync(version.Code, cancellationToken);
    }

    private static Result ValidateVersionDates(DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
    {
        if (effectiveTo is not null && effectiveTo <= effectiveFrom)
        {
            return Result.Failure(Error.Validation("Effective end must be after effective start."));
        }

        return Result.Success();
    }

    private static Result ValidateEntryInput(PricingCatalogEntryInputDto input, out SubscriptionSku sku)
    {
        sku = default;
        if (string.IsNullOrWhiteSpace(input.CountryCode) || input.CountryCode.Trim().Length != 2)
        {
            return Result.Failure(Error.Validation("Country code must be a 2-letter ISO code."));
        }

        if (string.IsNullOrWhiteSpace(input.CurrencyCode) || input.CurrencyCode.Trim().Length != 3)
        {
            return Result.Failure(Error.Validation("Currency code must be a 3-letter ISO code."));
        }

        if (!TryParseSku(input.SubscriptionSku, out sku))
        {
            return Result.Failure(Error.Validation("Subscription SKU is invalid."));
        }

        if (input.PlatformFeePercent is < 0 or > 100)
        {
            return Result.Failure(Error.Validation("Platform fee must be between 0 and 100."));
        }

        if (input.MonthlyAmountMajor is < 0)
        {
            return Result.Failure(Error.Validation("Monthly amount cannot be negative."));
        }

        return Result.Success();
    }

    private static PricingCatalogEntry CloneEntry(Guid versionId, PricingCatalogEntry source) =>
        new()
        {
            Id = Guid.NewGuid(),
            PricingCatalogVersionId = versionId,
            CountryCode = source.CountryCode,
            CurrencyCode = source.CurrencyCode,
            SubscriptionSku = source.SubscriptionSku,
            MonthlyAmountMajor = source.MonthlyAmountMajor,
            PlatformFeePercent = source.PlatformFeePercent,
            IsSelfServeEnabled = source.IsSelfServeEnabled,
        };

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
