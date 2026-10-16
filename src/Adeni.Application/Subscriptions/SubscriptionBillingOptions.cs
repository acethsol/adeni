namespace Adeni.Application.Subscriptions;

/// <summary>
/// Provider secrets for subscription billing webhooks (binds Paystack section until live multi-provider wiring).
/// </summary>
public sealed class SubscriptionBillingOptions
{
    public const string PaystackSectionName = "Paystack";

    public string WebhookSecret { get; set; } = string.Empty;
}
