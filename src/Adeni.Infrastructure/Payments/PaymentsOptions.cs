namespace Adeni.Infrastructure.Payments;

public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    public string Provider { get; set; } = "Stub";

    public string StubCheckoutBaseUrl { get; set; } = "/checkout/stub";

    public string ReceiptBaseUrl { get; set; } = "/checkout/receipt";
}

public sealed class PaystackOptions
{
    public const string SectionName = "Paystack";

    public string SecretKey { get; set; } = string.Empty;

    public string PublicKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.paystack.co";
}

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";

    public string SecretKey { get; set; } = string.Empty;

    public string PublishableKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.stripe.com";

    /// <summary>Max age (seconds) for Stripe-Signature timestamp; 0 disables skew check.</summary>
    public int WebhookToleranceSeconds { get; set; } = 300;
}
