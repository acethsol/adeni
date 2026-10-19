namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Common;
using Adeni.Domain.Subscriptions;

public sealed record PublicSkuPriceDto(string Sku, decimal? MonthlyAmountMajor);

public sealed record PublicCountryPricingDto(
    string CountryCode,
    string CurrencyCode,
    string PricingCatalogVersion,
    bool SelfServeEnabled,
    decimal PlatformFeePercent,
    IReadOnlyList<PublicSkuPriceDto> Skus);

public sealed record PricingCatalogVersionSummaryDto(
    Guid Id,
    string Code,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Status,
    bool IsActive);

public sealed record PricingCatalogEntryDto(
    string CountryCode,
    string CurrencyCode,
    string SubscriptionSku,
    decimal? MonthlyAmountMajor,
    decimal PlatformFeePercent,
    bool IsSelfServeEnabled);

public sealed record PricingCatalogEntryDiffDto(
    string CountryCode,
    string SubscriptionSku,
    decimal? ActiveMonthlyAmountMajor,
    decimal? MonthlyAmountMajor);

public sealed record PricingCatalogVersionDetailDto(
    Guid Id,
    string Code,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Status,
    bool IsActive,
    IReadOnlyList<PricingCatalogEntryDto> Entries,
    IReadOnlyList<PricingCatalogEntryDiffDto>? DiffFromActive);

public interface IPricingCatalogService
{
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);

    Task RefreshCacheAsync(CancellationToken cancellationToken = default);

    string GetActiveVersionCode();

    decimal? GetMonthlyMajor(string? countryCode, SubscriptionSku sku);

    Task<Result<PublicCountryPricingDto>> GetPublicCountryPricingAsync(
        string? countryCode,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PricingCatalogVersionSummaryDto>>> ListVersionsAsync(
        CancellationToken cancellationToken = default);

    Task<Result<PricingCatalogVersionDetailDto>> GetVersionAsync(
        string code,
        CancellationToken cancellationToken = default);
}
