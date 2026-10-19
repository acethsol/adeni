namespace Adeni.Api.Extensions;

using Adeni.Application.Auth;
using Adeni.Infrastructure.Finance;

public static class ProductionConfigurationExtensions
{
    public static WebApplicationBuilder ValidateProductionConfiguration(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsStaging() && !builder.Environment.IsProduction())
        {
            return builder;
        }

        var configuration = builder.Configuration;
        var errors = new List<string>();

        var auth0 = configuration.GetSection(Auth0Options.SectionName).Get<Auth0Options>();
        if (auth0?.Enabled != true)
        {
            errors.Add("Auth0:Enabled must be true in Staging/Production.");
        }

        var cors = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>();
        if (cors?.AllowedOrigins is not { Length: > 0 })
        {
            errors.Add("Cors:AllowedOrigins must contain at least one origin in Staging/Production.");
        }

        var paymentsProvider = configuration.GetSection(FinanceOptions.SectionName)["Provider"]
            ?? configuration.GetSection(PaymentsOptions.SectionName)["Provider"]
            ?? "Stub";
        var paystackWebhookSecret = configuration.GetSection(PaystackOptions.SectionName)["WebhookSecret"];
        var stripeWebhookSecret = configuration.GetSection(StripeOptions.SectionName)["WebhookSecret"];

        if (string.Equals(paymentsProvider, "Paystack", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(paystackWebhookSecret))
        {
            errors.Add("Paystack:WebhookSecret must be set when Payments:Provider is Paystack.");
        }

        if (string.Equals(paymentsProvider, "Stripe", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(stripeWebhookSecret))
        {
            errors.Add("Stripe:WebhookSecret must be set when Payments:Provider is Stripe.");
        }

        if (string.Equals(paymentsProvider, "MarketDefault", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(paystackWebhookSecret) || string.IsNullOrWhiteSpace(stripeWebhookSecret)))
        {
            errors.Add(
                "Paystack:WebhookSecret and Stripe:WebhookSecret must both be set when Payments:Provider is MarketDefault.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Production configuration validation failed:" + Environment.NewLine
                + string.Join(Environment.NewLine, errors));
        }

        return builder;
    }
}
