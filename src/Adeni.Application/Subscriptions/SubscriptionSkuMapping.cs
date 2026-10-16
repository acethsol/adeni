namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Subscriptions;

public static class SubscriptionSkuMapping
{
    public static string ToApiValue(SubscriptionSku sku) =>
        sku switch
        {
            SubscriptionSku.Professional => "professional",
            SubscriptionSku.Business => "business",
            _ => "starter",
        };

    public static SubscriptionSku FromApiValue(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "professional" or "pro" => SubscriptionSku.Professional,
            "business" => SubscriptionSku.Business,
            _ => SubscriptionSku.Starter,
        };

    public static SubscriptionSku FromLegacyTier(SubscriptionTier tier) =>
        tier switch
        {
            SubscriptionTier.Pro => SubscriptionSku.Professional,
            SubscriptionTier.Business => SubscriptionSku.Business,
            _ => SubscriptionSku.Starter,
        };

    public static SubscriptionTier ToLegacyTier(SubscriptionSku sku) =>
        sku switch
        {
            SubscriptionSku.Professional => SubscriptionTier.Pro,
            SubscriptionSku.Business => SubscriptionTier.Business,
            _ => SubscriptionTier.Free,
        };

    /** Legacy portal/API tier string (free | pro | business). */
    public static string ToLegacyTierApiValue(SubscriptionSku sku) =>
        SubscriptionTierMapping.ToApiValue(ToLegacyTier(sku));
}
