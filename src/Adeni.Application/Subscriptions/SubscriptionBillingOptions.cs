namespace Adeni.Application.Subscriptions;

/// <summary>
/// SaaS subscription billing provider selection and Paystack plan codes (staging/prod via config / Key Vault).
/// Paystack API secrets bind from the Paystack configuration section when not set here.
/// </summary>
public sealed class SubscriptionBillingOptions
{
    public const string SectionName = "SubscriptionBilling";

    /// <summary>Stub | Paystack | MarketDefault (Lagos → Paystack, Ottawa → not ready).</summary>
    public string Provider { get; set; } = "Stub";

    public SubscriptionBillingPaystackOptions Paystack { get; set; } = new();
}

public sealed class SubscriptionBillingPaystackOptions
{
    public string SecretKey { get; set; } = string.Empty;

    public string WebhookSecret { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.paystack.co";

    /// <summary>Paystack plan_code for Professional (pro). Optional — falls back to one-time initialize amount.</summary>
    public string ProfessionalPlanCode { get; set; } = string.Empty;

    /// <summary>Paystack plan_code for Business tier. Optional — falls back to one-time initialize amount.</summary>
    public string BusinessPlanCode { get; set; } = string.Empty;
}
