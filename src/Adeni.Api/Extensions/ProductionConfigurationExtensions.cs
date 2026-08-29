namespace Adeni.Api.Extensions;

using Adeni.Application.Auth;
using Adeni.Infrastructure.Payments;

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

        var paymentsProvider = configuration.GetSection(PaymentsOptions.SectionName)["Provider"] ?? "Stub";
        if (string.Equals(paymentsProvider, "Paystack", StringComparison.OrdinalIgnoreCase))
        {
            var webhookSecret = configuration.GetSection(PaystackOptions.SectionName)["WebhookSecret"];
            if (string.IsNullOrWhiteSpace(webhookSecret))
            {
                errors.Add("Paystack:WebhookSecret must be set when Payments:Provider is Paystack.");
            }
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
