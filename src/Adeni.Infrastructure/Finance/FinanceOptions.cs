namespace Adeni.Infrastructure.Finance;

public sealed class FinanceOptions
{
    public const string SectionName = "Finance";

    public string Provider { get; set; } = "Stub";

    public string StubCheckoutBaseUrl { get; set; } = "/checkout/stub";

    public string ReceiptBaseUrl { get; set; } = "/checkout/receipt";
}

/// <summary>Legacy appsettings section name (still bound into <see cref="FinanceOptions"/>).</summary>
public static class PaymentsOptions
{
    public const string SectionName = "Payments";
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

    public int WebhookToleranceSeconds { get; set; } = 300;
}
