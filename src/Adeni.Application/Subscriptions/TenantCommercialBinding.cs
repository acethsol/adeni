namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;

/// <summary>
/// Binds catalog version and agreed list price on first paid subscription activation.
/// </summary>
public static class TenantCommercialBinding
{
    public static bool IsBoundListPriceActive(Tenant tenant, SubscriptionSku sku, DateTimeOffset utcNow)
    {
        if (tenant.AgreedMonthlyAmount is not > 0
            || string.IsNullOrWhiteSpace(tenant.AgreedCurrencyCode))
        {
            return false;
        }

        if (tenant.PriceLockEndsAt is { } lockEnd && lockEnd <= utcNow)
        {
            return false;
        }

        return tenant.SubscriptionSku == sku;
    }

    public static void BindOnFirstPaidActivation(
        Tenant tenant,
        TenantPricingResolution pricing,
        DateTimeOffset utcNow,
        DateTimeOffset? priceLockEndsAt = null)
    {
        if (tenant.AgreedMonthlyAmount is > 0)
        {
            return;
        }

        if (pricing.ListPriceMajor is not { } listMajor || listMajor <= 0)
        {
            return;
        }

        tenant.AgreedMonthlyAmount = listMajor;
        tenant.AgreedCurrencyCode = pricing.Currency;
        tenant.PricingCatalogVersionCode = pricing.PricingCatalogVersion;
        tenant.PriceLockEndsAt = priceLockEndsAt;
    }
}
