namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Subscriptions;

public static class SubscriptionTierMapping
{
    public static string ToApiValue(SubscriptionTier tier) =>
        tier switch
        {
            SubscriptionTier.Pro => "pro",
            SubscriptionTier.Business => "business",
            _ => "free",
        };

    public static SubscriptionTier FromApiValue(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "pro" => SubscriptionTier.Pro,
            "business" => SubscriptionTier.Business,
            _ => SubscriptionTier.Free,
        };
}
