namespace Adeni.Infrastructure.Subscriptions;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Resolves tenant SaaS list price: admin override → bound agreed price → active catalog.
/// </summary>
public sealed class TenantPricingResolver(
    AdeniDbContext dbContext,
    IPricingCatalogService pricingCatalog) : ITenantPricingResolver
{
    public async Task<string> ResolvePrimaryBillingCountryAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var map = await ResolvePrimaryBillingCountryByTenantAsync(cancellationToken);
        return map.TryGetValue(tenantId, out var country)
            ? country
            : SubscriptionCatalogPrices.DefaultCountryCode;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ResolvePrimaryBillingCountryByTenantAsync(
        CancellationToken cancellationToken = default)
    {
        var primaryMarkets = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.IsPrimary && x.IsActive)
            .Select(x => new { x.TenantId, x.MarketId })
            .ToListAsync(cancellationToken);

        var marketCountries = await dbContext.CatalogMarkets
            .AsNoTracking()
            .Select(m => new { m.Id, m.CountryCode })
            .ToListAsync(cancellationToken);

        return primaryMarkets.ToDictionary(
            x => x.TenantId,
            x => marketCountries.FirstOrDefault(m => m.Id == x.MarketId)?.CountryCode
                ?? SubscriptionCatalogPrices.DefaultCountryCode);
    }

    public TenantPricingResolution ResolveForTenant(
        Tenant tenant,
        string billingCountryCode,
        SubscriptionSku? skuOverride = null,
        DateTimeOffset? utcNow = null)
    {
        var now = utcNow ?? DateTimeOffset.UtcNow;
        var sku = skuOverride ?? tenant.SubscriptionSku;
        var country = NormalizeCountry(billingCountryCode);
        var currency = SubscriptionCatalogPrices.CurrencyForCountry(country);
        var activeCatalogMajor = pricingCatalog.GetMonthlyMajor(country, sku);
        var catalogVersionForDisplay = pricingCatalog.GetActiveVersionCode();

        var listMajor = ResolveListPriceMajor(tenant, sku, activeCatalogMajor, currency, now);
        if (listMajor is not null)
        {
            currency = ResolveCurrency(tenant, currency);
        }

        var boundVersion = tenant.PricingCatalogVersionCode;
        var resolutionVersion = TenantCommercialBinding.IsBoundListPriceActive(tenant, sku, now)
            && !string.IsNullOrWhiteSpace(boundVersion)
            ? boundVersion
            : catalogVersionForDisplay;

        decimal? effectiveMajor = listMajor is null
            ? null
            : ApplyTenantDiscount(listMajor.Value, tenant);

        return new TenantPricingResolution(
            country,
            currency,
            activeCatalogMajor,
            listMajor,
            effectiveMajor,
            resolutionVersion,
            tenant.AgreedMonthlyAmount,
            boundVersion);
    }

    public long? ToAmountMinor(decimal? amountMajor, string currency) =>
        amountMajor is null or <= 0
            ? null
            : SubscriptionCatalogPrices.ToAmountMinorFromMajor(amountMajor.Value, currency);

    private static string NormalizeCountry(string? countryCode) =>
        string.IsNullOrWhiteSpace(countryCode)
            ? SubscriptionCatalogPrices.DefaultCountryCode
            : countryCode.Trim().ToUpperInvariant();

    private static decimal? ResolveListPriceMajor(
        Tenant tenant,
        SubscriptionSku sku,
        decimal? activeCatalogMajor,
        string defaultCurrency,
        DateTimeOffset utcNow)
    {
        var customAmount = tenant.CustomSaasMonthlyAmount;
        var customSku = tenant.CustomSaasSku;
        if (customAmount is > 0
            && !string.IsNullOrWhiteSpace(tenant.CustomSaasCurrency)
            && (customSku is null || customSku == sku))
        {
            return customAmount;
        }

        if (TenantCommercialBinding.IsBoundListPriceActive(tenant, sku, utcNow))
        {
            return tenant.AgreedMonthlyAmount;
        }

        return activeCatalogMajor;
    }

    private static string ResolveCurrency(Tenant tenant, string defaultCurrency)
    {
        if (tenant.CustomSaasMonthlyAmount is > 0 && !string.IsNullOrWhiteSpace(tenant.CustomSaasCurrency))
        {
            return tenant.CustomSaasCurrency.Trim().ToUpperInvariant();
        }

        if (tenant.AgreedMonthlyAmount is > 0 && !string.IsNullOrWhiteSpace(tenant.AgreedCurrencyCode))
        {
            return tenant.AgreedCurrencyCode.Trim().ToUpperInvariant();
        }

        return defaultCurrency;
    }

    private static decimal ApplyTenantDiscount(decimal listPrice, Tenant tenant)
    {
        if (tenant.DiscountCyclesRemaining is 0)
        {
            return listPrice;
        }

        if (tenant.DiscountFixedAmount is > 0)
        {
            return Math.Max(0, listPrice - tenant.DiscountFixedAmount.Value);
        }

        if (tenant.DiscountPercent is > 0)
        {
            return Math.Max(0, listPrice * (1 - tenant.DiscountPercent.Value / 100m));
        }

        return listPrice;
    }
}
