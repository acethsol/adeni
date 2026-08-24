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
