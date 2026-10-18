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
            "starter" => SubscriptionSku.Starter,
            _ => SubscriptionSku.Starter,
        };
}
