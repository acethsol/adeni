namespace Adeni.Domain.Subscriptions;

public sealed class PricingCatalogVersion
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public DateTimeOffset EffectiveFrom { get; set; }

    public DateTimeOffset? EffectiveTo { get; set; }

    public PricingCatalogVersionStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<PricingCatalogEntry> Entries { get; set; } = [];
}
