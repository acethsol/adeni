namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;

public sealed record TenantPricingResolution(
    string BillingCountryCode,
    string Currency,
    decimal? CatalogListPriceMajor,
    decimal? ListPriceMajor,
    decimal? EffectiveMonthlyAmountMajor,
    string PricingCatalogVersion);

public interface ITenantPricingResolver
{
    Task<string> ResolvePrimaryBillingCountryAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, string>> ResolvePrimaryBillingCountryByTenantAsync(
        CancellationToken cancellationToken = default);

    TenantPricingResolution ResolveForTenant(
        Tenant tenant,
        string billingCountryCode,
        SubscriptionSku? skuOverride = null);

    long? ToAmountMinor(decimal? amountMajor, string currency);
}
