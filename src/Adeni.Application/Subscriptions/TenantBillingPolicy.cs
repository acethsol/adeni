namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;

public static class TenantBillingDefaults
{
    public const int DefaultTrialDays = 30;

    public static SubscriptionSku DefaultTrialSku => SubscriptionSku.Professional;
}

public static class TenantBillingPolicy
{
    public static SubscriptionSku ResolveEffectiveSku(Tenant tenant, DateTimeOffset? utcNow = null)
    {
        var now = utcNow ?? DateTimeOffset.UtcNow;

        if (tenant.BillingStatus == TenantBillingStatus.Trialing
            && tenant.TrialEndsAt is { } trialEnd
            && trialEnd > now)
        {
            return tenant.TrialSku ?? TenantBillingDefaults.DefaultTrialSku;
        }

        if (tenant.BillingStatus is TenantBillingStatus.Suspended or TenantBillingStatus.Canceled)
        {
            return SubscriptionSku.Starter;
        }

        var sku = tenant.SubscriptionSku;
        if (sku == SubscriptionSku.Starter
            && tenant.SubscriptionTier is not SubscriptionTier.Free)
        {
            sku = SubscriptionSkuMapping.FromLegacyTier(tenant.SubscriptionTier);
        }

        return sku;
    }

    public static void ApplySkuToLegacyTier(Tenant tenant)
    {
        tenant.SubscriptionTier = SubscriptionSkuMapping.ToLegacyTier(tenant.SubscriptionSku);
    }

    public static void StartDefaultTrial(Tenant tenant, DateTimeOffset now)
    {
        tenant.SubscriptionSku = SubscriptionSku.Starter;
        GrantTrial(tenant, TenantBillingDefaults.DefaultTrialDays, TenantBillingDefaults.DefaultTrialSku, now);
        ApplySkuToLegacyTier(tenant);
    }

    public static void GrantTrial(
        Tenant tenant,
        int trialDays,
        SubscriptionSku? trialSku,
        DateTimeOffset now)
    {
        if (trialDays <= 0)
        {
            EndTrial(tenant);
            return;
        }

        tenant.BillingStatus = TenantBillingStatus.Trialing;
        tenant.TrialSku = trialSku ?? TenantBillingDefaults.DefaultTrialSku;
        tenant.TrialEndsAt = now.AddDays(trialDays);
    }

    public static void EndTrial(Tenant tenant)
    {
        tenant.BillingStatus = TenantBillingStatus.Active;
        tenant.TrialEndsAt = null;
        tenant.TrialSku = null;
    }

    public static void ClearDiscounts(Tenant tenant)
    {
        tenant.DiscountPercent = null;
        tenant.DiscountFixedAmount = null;
        tenant.DiscountCurrency = null;
        tenant.DiscountCyclesRemaining = null;
    }

    public static void ClearCustomSaasPrice(Tenant tenant)
    {
        tenant.CustomSaasMonthlyAmount = null;
        tenant.CustomSaasCurrency = null;
        tenant.CustomSaasSku = null;
    }
}
