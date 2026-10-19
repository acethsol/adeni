namespace Adeni.Infrastructure.Subscriptions;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Subscriptions;

internal static class PricingCatalogSeedData
{
    internal static readonly Guid LaunchVersionId = Guid.Parse("a1b2c3d4-e5f6-4789-a012-202601010001");

    internal const string LaunchVersionCode = PricingCatalogMetadata.ActiveVersionCode;

    internal static readonly DateTimeOffset LaunchEffectiveFrom =
        DateTimeOffset.Parse("2026-10-19T00:00:00+00:00");

    internal static PricingCatalogVersion CreateLaunchVersion(DateTimeOffset createdAt) =>
        new()
        {
            Id = LaunchVersionId,
            Code = LaunchVersionCode,
            EffectiveFrom = LaunchEffectiveFrom,
            EffectiveTo = null,
            Status = PricingCatalogVersionStatus.Active,
            CreatedAt = createdAt,
            Entries = CreateLaunchEntries(LaunchVersionId),
        };

    private static List<PricingCatalogEntry> CreateLaunchEntries(Guid versionId) =>
    [
        Entry(versionId, "NG", "NGN", SubscriptionSku.Starter, 35_000m, true),
        Entry(versionId, "NG", "NGN", SubscriptionSku.Professional, 55_000m, true),
        Entry(versionId, "NG", "NGN", SubscriptionSku.Business, 95_000m, true),
        Entry(versionId, "CA", "CAD", SubscriptionSku.Starter, 99m, true),
        Entry(versionId, "CA", "CAD", SubscriptionSku.Professional, 149m, true),
        Entry(versionId, "CA", "CAD", SubscriptionSku.Business, 249m, true),
        Entry(versionId, "US", "USD", SubscriptionSku.Starter, null, false),
        Entry(versionId, "US", "USD", SubscriptionSku.Professional, null, false),
        Entry(versionId, "US", "USD", SubscriptionSku.Business, null, false),
    ];

    private static PricingCatalogEntry Entry(
        Guid versionId,
        string country,
        string currency,
        SubscriptionSku sku,
        decimal? amount,
        bool selfServe) =>
        new()
        {
            Id = Guid.NewGuid(),
            PricingCatalogVersionId = versionId,
            CountryCode = country,
            CurrencyCode = currency,
            SubscriptionSku = sku,
            MonthlyAmountMajor = amount,
            PlatformFeePercent = 0m,
            IsSelfServeEnabled = selfServe,
        };
}
