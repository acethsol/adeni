namespace Adeni.Application.Admin;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;

public sealed record AdminCommercialTermsSnapshot(
    string SubscriptionSku,
    string BillingStatus,
    string? TrialSku,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? PaidThrough,
    decimal? DiscountPercent,
    decimal? DiscountFixedAmount,
    string? DiscountCurrency,
    int? DiscountCyclesRemaining,
    string? BillingCompReason,
    decimal? CustomSaasMonthlyAmount,
    string? CustomSaasCurrency,
    string? CustomSaasSku)
{
    public static AdminCommercialTermsSnapshot FromTenant(Tenant tenant) =>
        new(
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
                : null);

    public bool EqualsCommercialTerms(AdminCommercialTermsSnapshot other) =>
        SubscriptionSku == other.SubscriptionSku
        && BillingStatus == other.BillingStatus
        && TrialSku == other.TrialSku
        && TrialEndsAt == other.TrialEndsAt
        && PaidThrough == other.PaidThrough
        && DiscountPercent == other.DiscountPercent
        && DiscountFixedAmount == other.DiscountFixedAmount
        && DiscountCurrency == other.DiscountCurrency
        && DiscountCyclesRemaining == other.DiscountCyclesRemaining
        && CustomSaasMonthlyAmount == other.CustomSaasMonthlyAmount
        && CustomSaasCurrency == other.CustomSaasCurrency
        && CustomSaasSku == other.CustomSaasSku;
}
