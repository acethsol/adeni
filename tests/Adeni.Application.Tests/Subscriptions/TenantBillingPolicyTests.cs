namespace Adeni.Application.Tests.Subscriptions;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;

public sealed class TenantBillingPolicyTests
{
    [Fact]
    public void ResolveEffectiveSku_uses_trial_sku_while_trialing()
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = new Tenant
        {
            SubscriptionSku = SubscriptionSku.Starter,
            BillingStatus = TenantBillingStatus.Trialing,
            TrialSku = SubscriptionSku.Professional,
            TrialEndsAt = now.AddDays(30),
        };

        var sku = TenantBillingPolicy.ResolveEffectiveSku(tenant, now);

        Assert.Equal(SubscriptionSku.Professional, sku);
    }

    [Fact]
    public void ResolveEffectiveSku_falls_back_to_starter_when_suspended()
    {
        var tenant = new Tenant
        {
            SubscriptionSku = SubscriptionSku.Business,
            BillingStatus = TenantBillingStatus.Suspended,
        };

        var sku = TenantBillingPolicy.ResolveEffectiveSku(tenant);

        Assert.Equal(SubscriptionSku.Starter, sku);
    }

    [Fact]
    public void GrantTrial_zero_days_ends_trial()
    {
        var tenant = new Tenant
        {
            BillingStatus = TenantBillingStatus.Trialing,
            TrialSku = SubscriptionSku.Professional,
            TrialEndsAt = DateTimeOffset.UtcNow.AddDays(10),
        };

        TenantBillingPolicy.GrantTrial(tenant, 0, null, DateTimeOffset.UtcNow);

        Assert.Equal(TenantBillingStatus.Active, tenant.BillingStatus);
        Assert.Null(tenant.TrialEndsAt);
        Assert.Null(tenant.TrialSku);
    }

    [Fact]
    public void StartDefaultTrial_sets_default_day_professional_trial()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var tenant = new Tenant();

        TenantBillingPolicy.StartDefaultTrial(tenant, now);

        Assert.Equal(TenantBillingStatus.Trialing, tenant.BillingStatus);
        Assert.Equal(SubscriptionSku.Starter, tenant.SubscriptionSku);
        Assert.Equal(SubscriptionSku.Professional, tenant.TrialSku);
        Assert.Equal(now.AddDays(TenantBillingDefaults.DefaultTrialDays), tenant.TrialEndsAt);
    }
}
