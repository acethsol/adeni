namespace Adeni.Infrastructure.Admin;

using System.Text.Json;
using System.Text.RegularExpressions;
using Adeni.Application.Abstractions;
using Adeni.Application.Admin;
using Adeni.Application.Caching;
using Adeni.Application.Markets;
using Adeni.Domain.Auditing;
using Adeni.Domain.Catalog;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Markets;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed partial class AdminMarketService(
    AdeniDbContext dbContext,
    IAuditLogWriter auditLogWriter,
    ICorrelationContext correlationContext,
    IMarketCatalogLoader marketCatalogLoader,
    ICacheService cache) : IAdminMarketService
{
    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "fr", "es", "pt",
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<IReadOnlyList<AdminMarketResponse>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.CatalogMarkets
            .AsNoTracking()
            .OrderBy(market => market.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(MapResponse).ToArray();
    }

    public async Task<Result<AdminMarketResponse>> CreateAsync(
        CreateMarketRequest request,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreate(request);
        if (validation.IsFailure)
        {
            return Result.Failure<AdminMarketResponse>(validation.Error);
        }

        var normalizedId = request.Id.Trim().ToLowerInvariant();
        if (await dbContext.CatalogMarkets.AnyAsync(market => market.Id == normalizedId, cancellationToken))
        {
            return Result.Failure<AdminMarketResponse>(Error.Conflict("A market with this id already exists."));
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new CatalogMarket
        {
            Id = normalizedId,
            Name = request.Name.Trim(),
            CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
            Currency = request.Currency.Trim().ToUpperInvariant(),
            TimeZoneId = request.TimeZoneId.Trim(),
            DefaultLat = request.DefaultLat,
            DefaultLng = request.DefaultLng,
            LanguagesJson = JsonSerializer.Serialize(NormalizeLanguages(request.Languages), SerializerOptions),
            IsLive = request.IsLive,
            LaunchNote = string.IsNullOrWhiteSpace(request.LaunchNote) ? null : request.LaunchNote.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.CatalogMarkets.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateCatalogAsync(cancellationToken);

        await auditLogWriter.WriteAsync(new AuditEntry(
            Guid.NewGuid(),
            adminId,
            AuditActions.MarketCreated,
            "market",
            entity.Id,
            correlationContext.CorrelationId,
            now,
            null),
            cancellationToken);

        return Result.Success(MapResponse(entity));
    }

    public async Task<Result<AdminMarketResponse>> UpdateAsync(
        string id,
        UpdateMarketRequest request,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = id.Trim().ToLowerInvariant();
        var entity = await dbContext.CatalogMarkets.FirstOrDefaultAsync(
            market => market.Id == normalizedId,
            cancellationToken);

        if (entity is null)
        {
            return Result.Failure<AdminMarketResponse>(Error.NotFound("Market"));
        }

        var validation = ValidateUpdate(request);
        if (validation.IsFailure)
        {
            return Result.Failure<AdminMarketResponse>(validation.Error);
        }

        entity.Name = request.Name.Trim();
        entity.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        entity.Currency = request.Currency.Trim().ToUpperInvariant();
        entity.TimeZoneId = request.TimeZoneId.Trim();
        entity.DefaultLat = request.DefaultLat;
        entity.DefaultLng = request.DefaultLng;
        entity.LanguagesJson = JsonSerializer.Serialize(NormalizeLanguages(request.Languages), SerializerOptions);
        entity.LaunchNote = string.IsNullOrWhiteSpace(request.LaunchNote) ? null : request.LaunchNote.Trim();
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateCatalogAsync(cancellationToken);

        await auditLogWriter.WriteAsync(new AuditEntry(
            Guid.NewGuid(),
            adminId,
            AuditActions.MarketUpdated,
            "market",
            entity.Id,
            correlationContext.CorrelationId,
            entity.UpdatedAt,
            null),
            cancellationToken);

        return Result.Success(MapResponse(entity));
    }

    public async Task<Result<Unit>> SetLiveAsync(
        string id,
        bool isLive,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        var normalizedId = id.Trim().ToLowerInvariant();
        var entity = await dbContext.CatalogMarkets.FirstOrDefaultAsync(
            market => market.Id == normalizedId,
            cancellationToken);

        if (entity is null)
        {
            return Result.Failure<Unit>(Error.NotFound("Market"));
        }

        entity.IsLive = isLive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateCatalogAsync(cancellationToken);

        await auditLogWriter.WriteAsync(new AuditEntry(
            Guid.NewGuid(),
            adminId,
            AuditActions.MarketLiveToggled,
            "market",
            entity.Id,
            correlationContext.CorrelationId,
            entity.UpdatedAt,
            $"{{\"isLive\":{isLive.ToString().ToLowerInvariant()}}}"),
            cancellationToken);

        return Result.Success(Unit.Value);
    }

    private async Task InvalidateCatalogAsync(CancellationToken cancellationToken)
    {
        await cache.RemoveAsync(CacheKeys.MarketsAll, cancellationToken);
        await marketCatalogLoader.InvalidateAndReloadAsync(cancellationToken);
    }

    private static Result<Unit> ValidateCreate(CreateMarketRequest request)
    {
        if (!MarketIdPattern().IsMatch(request.Id.Trim()))
        {
            return Result.Failure<Unit>(Error.Validation("Market id must be 2-32 lowercase letters, numbers, or hyphens."));
        }

        return ValidateCommon(
            request.Name,
            request.CountryCode,
            request.Currency,
            request.TimeZoneId,
            request.DefaultLat,
            request.DefaultLng,
            request.Languages);
    }

    private static Result<Unit> ValidateUpdate(UpdateMarketRequest request) =>
        ValidateCommon(
            request.Name,
            request.CountryCode,
            request.Currency,
            request.TimeZoneId,
            request.DefaultLat,
            request.DefaultLng,
            request.Languages);

    private static Result<Unit> ValidateCommon(
        string name,
        string countryCode,
        string currency,
        string timeZoneId,
        double defaultLat,
        double defaultLng,
        IReadOnlyList<string> languages)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2)
        {
            return Result.Failure<Unit>(Error.Validation("Market name must be at least 2 characters."));
        }

        if (!CountryCodePattern().IsMatch(countryCode.Trim()))
        {
            return Result.Failure<Unit>(Error.Validation("Country code must be a 2-letter ISO code."));
        }

        if (!CurrencyPattern().IsMatch(currency.Trim()))
        {
            return Result.Failure<Unit>(Error.Validation("Currency must be a 3-letter ISO code."));
        }

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return Result.Failure<Unit>(Error.Validation("Time zone is required."));
        }

        if (defaultLat is < -90 or > 90 || defaultLng is < -180 or > 180)
        {
            return Result.Failure<Unit>(Error.Validation("Default location coordinates are out of range."));
        }

        if (languages.Count == 0)
        {
            return Result.Failure<Unit>(Error.Validation("At least one language is required."));
        }

        if (languages.Any(language => !SupportedLanguages.Contains(language)))
        {
            return Result.Failure<Unit>(Error.Validation("Languages must be one of: en, fr, es, pt."));
        }

        return Result.Success(Unit.Value);
    }

    private static IReadOnlyList<string> NormalizeLanguages(IReadOnlyList<string> languages) =>
        languages
            .Select(language => language.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static AdminMarketResponse MapResponse(CatalogMarket market)
    {
        var languages = JsonSerializer.Deserialize<List<string>>(market.LanguagesJson, SerializerOptions)
            ?? [];

        return new AdminMarketResponse(
            market.Id,
            market.Name,
            market.CountryCode,
            market.Currency,
            market.TimeZoneId,
            market.DefaultLat,
            market.DefaultLng,
            languages,
            market.IsLive,
            market.LaunchNote,
            market.UpdatedAt);
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{0,30}[a-z0-9]$|^[a-z]{2}$")]
    private static partial Regex MarketIdPattern();

    [GeneratedRegex("^[A-Za-z]{2}$")]
    private static partial Regex CountryCodePattern();

    [GeneratedRegex("^[A-Za-z]{3}$")]
    private static partial Regex CurrencyPattern();
}
