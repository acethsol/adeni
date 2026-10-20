namespace Adeni.Domain.Tenancy;

using Adeni.Domain.Subscriptions;

public sealed class Tenant
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public TenantStatus Status { get; set; } = TenantStatus.Draft;

    public SubscriptionSku SubscriptionSku { get; set; } = SubscriptionSku.Starter;

    public TenantBillingStatus BillingStatus { get; set; } = TenantBillingStatus.Trialing;

    public SubscriptionSku? TrialSku { get; set; }

    public DateTimeOffset? TrialEndsAt { get; set; }

    public DateTimeOffset? PaidThrough { get; set; }

    public decimal? DiscountPercent { get; set; }

    public decimal? DiscountFixedAmount { get; set; }

    public string? DiscountCurrency { get; set; }

    public int? DiscountCyclesRemaining { get; set; }

    public string? BillingCompReason { get; set; }

    /// <summary>Admin override of catalog monthly SaaS price for this tenant (before discounts).</summary>
    public decimal? CustomSaasMonthlyAmount { get; set; }

    public string? CustomSaasCurrency { get; set; }

    public SubscriptionSku? CustomSaasSku { get; set; }

    /// <summary>List price frozen at first paid subscription activation (before discounts).</summary>
    public decimal? AgreedMonthlyAmount { get; set; }

    public string? AgreedCurrencyCode { get; set; }

    public string? PricingCatalogVersionCode { get; set; }

    public DateTimeOffset? PriceLockEndsAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }
}
