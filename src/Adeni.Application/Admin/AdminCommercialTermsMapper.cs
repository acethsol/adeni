namespace Adeni.Application.Admin;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Tenancy;

public static class AdminCommercialTermsMapper
{
    public static AdminTenantCommercialTermsResponse ToResponse(
        Tenant tenant,
        TenantPricingResolution? pricing = null) =>
        new(
            tenant.Id,
            SubscriptionSkuMapping.ToApiValue(tenant.SubscriptionSku),
            TenantBillingStatusMapping.ToApiValue(tenant.BillingStatus),
            tenant.TrialSku is { } trialSku
                ? SubscriptionSkuMapping.ToApiValue(trialSku)
                : null,
            tenant.TrialEndsAt,
            tenant.PaidThrough,
            tenant.DiscountPercent,
            tenant.DiscountFixedAmount,
            tenant.DiscountCurrency,
            tenant.DiscountCyclesRemaining,
            tenant.BillingCompReason,
            tenant.CustomSaasMonthlyAmount,
            tenant.CustomSaasCurrency,
            tenant.CustomSaasSku is { } customSku
                ? SubscriptionSkuMapping.ToApiValue(customSku)
                : null,
            pricing?.BillingCountryCode,
            pricing?.CatalogListPriceMajor,
            pricing?.EffectiveMonthlyAmountMajor,
            pricing?.PricingCatalogVersion,
            pricing?.Currency,
            pricing?.AgreedMonthlyAmountMajor ?? tenant.AgreedMonthlyAmount,
            pricing?.BoundPricingCatalogVersion ?? tenant.PricingCatalogVersionCode,
            tenant.PriceLockEndsAt);
}
