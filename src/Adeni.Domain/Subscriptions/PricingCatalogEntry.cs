namespace Adeni.Domain.Subscriptions;

public sealed class PricingCatalogEntry
{
    public Guid Id { get; set; }

    public Guid PricingCatalogVersionId { get; set; }

    public string CountryCode { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public SubscriptionSku SubscriptionSku { get; set; }

    public decimal? MonthlyAmountMajor { get; set; }

    public decimal PlatformFeePercent { get; set; }

    public bool IsSelfServeEnabled { get; set; }

    public PricingCatalogVersion? Version { get; set; }
}
