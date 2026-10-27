namespace Adeni.Api.Extensions;

using Adeni.Application.Auth;
using Adeni.Application.Storage;
using Adeni.Application.Subscriptions;
using Adeni.Infrastructure.Notifications;
using Adeni.Infrastructure.Finance;
using Azure.Storage.Blobs;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

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

        var sql = configuration.GetConnectionString("AdeniDb");
        try
        {
            if (string.IsNullOrWhiteSpace(sql) || string.IsNullOrWhiteSpace(new SqlConnectionStringBuilder(sql).DataSource))
            {
                errors.Add("ConnectionStrings:AdeniDb must specify a SQL Server connection in Staging/Production.");
            }
        }
        catch (ArgumentException)
        {
            errors.Add("ConnectionStrings:AdeniDb is not a valid SQL Server connection string.");
        }

        var auth0 = configuration.GetSection(Auth0Options.SectionName).Get<Auth0Options>();
        if (auth0?.Enabled != true)
        {
            errors.Add("Auth0:Enabled must be true in Staging/Production.");
        }

        if (string.IsNullOrWhiteSpace(auth0?.Domain)
            || Uri.CheckHostName(auth0.Domain.TrimEnd('/')) == UriHostNameType.Unknown
            || auth0.Domain.Contains(':') || auth0.Domain.TrimEnd('/').Contains('/'))
        {
            errors.Add("Auth0:Domain must be a hostname without a scheme or path.");
        }

        RequireValue(auth0?.Audience, "Auth0:Audience");

        var cors = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>();
        if (cors?.AllowedOrigins is not { Length: > 0 })
        {
            errors.Add("Cors:AllowedOrigins must contain at least one origin in Staging/Production.");
        }
        else if (cors.AllowedOrigins.Any(origin => !IsPublicHttpsUrl(origin)
            || new Uri(origin).AbsolutePath != "/" || new Uri(origin).Query.Length > 0 || origin.EndsWith('/')))
        {
            errors.Add("Cors:AllowedOrigins must contain explicit HTTPS origins without paths, wildcards, or trailing slashes.");
        }

        RequireHttpsUrl(configuration["App:PublicWebBaseUrl"], "App:PublicWebBaseUrl");
        RequireHttpsUrl(configuration["Portal:PublicBaseUrl"], "Portal:PublicBaseUrl");

        var paymentsProvider = configuration.GetSection(FinanceOptions.SectionName)["Provider"]
            ?? configuration.GetSection(PaymentsOptions.SectionName)["Provider"]
            ?? "Stub";
        if (!new[] { "Paystack", "Stripe", "MarketDefault" }.Contains(paymentsProvider, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("Payments:Provider (or Finance:Provider override) must be Paystack, Stripe, or MarketDefault in Staging/Production; Stub is not allowed.");
        }

        var subscriptionProvider = configuration.GetSection(SubscriptionBillingOptions.SectionName)["Provider"] ?? "Stub";
        if (!new[] { "Paystack", "MarketDefault" }.Contains(subscriptionProvider, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add("SubscriptionBilling:Provider must be Paystack or MarketDefault in Staging/Production; Stub is not allowed.");
        }

        if (string.Equals(paymentsProvider, "Paystack", StringComparison.OrdinalIgnoreCase)
            || string.Equals(paymentsProvider, "MarketDefault", StringComparison.OrdinalIgnoreCase))
        {
            RequireValue(configuration["Paystack:SecretKey"], "Paystack:SecretKey");
            RequireHttpsUrl(configuration["Paystack:BaseUrl"] ?? new PaystackOptions().BaseUrl, "Paystack:BaseUrl");
        }

        if (string.Equals(paymentsProvider, "Stripe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(paymentsProvider, "MarketDefault", StringComparison.OrdinalIgnoreCase))
        {
            RequireValue(configuration["Stripe:SecretKey"], "Stripe:SecretKey");
            RequireHttpsUrl(configuration["Stripe:BaseUrl"] ?? new StripeOptions().BaseUrl, "Stripe:BaseUrl");
        }

        if (string.Equals(subscriptionProvider, "Paystack", StringComparison.OrdinalIgnoreCase)
            || string.Equals(subscriptionProvider, "MarketDefault", StringComparison.OrdinalIgnoreCase))
        {
            var billing = configuration.GetSection(SubscriptionBillingOptions.SectionName).Get<SubscriptionBillingOptions>() ?? new();
            RequireValue(string.IsNullOrWhiteSpace(billing.Paystack.SecretKey) ? configuration["Paystack:SecretKey"] : billing.Paystack.SecretKey,
                "SubscriptionBilling:Paystack:SecretKey (or Paystack:SecretKey fallback)");
            RequireValue(string.IsNullOrWhiteSpace(billing.Paystack.WebhookSecret) ? configuration["Paystack:WebhookSecret"] : billing.Paystack.WebhookSecret,
                "SubscriptionBilling:Paystack:WebhookSecret (or Paystack:WebhookSecret fallback)");
            RequireHttpsUrl(string.IsNullOrWhiteSpace(billing.Paystack.BaseUrl) ? configuration["Paystack:BaseUrl"] ?? new SubscriptionBillingPaystackOptions().BaseUrl : billing.Paystack.BaseUrl,
                "SubscriptionBilling:Paystack:BaseUrl");
        }

        var storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new();
        if (string.Equals(storage.Provider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            RequireValue(storage.AzureBlob.ConnectionString, "Storage:AzureBlob:ConnectionString");
            if (string.IsNullOrWhiteSpace(storage.AzureBlob.PublicContainer)
                || (storage.AzureBlob.PublicContainer is not ("$root" or "$web")
                    && (!Regex.IsMatch(storage.AzureBlob.PublicContainer, @"^[a-z0-9](?:[a-z0-9-]{1,61})[a-z0-9]$")
                        || storage.AzureBlob.PublicContainer.Contains("--"))))
            {
                errors.Add("Storage:AzureBlob:PublicContainer must be a valid Azure container name (3–63 lowercase letters, numbers, or single hyphens; or $root/$web).");
            }
            if (!string.IsNullOrWhiteSpace(storage.AzureBlob.ConnectionString))
            {
                try
                {
                    var client = new BlobServiceClient(storage.AzureBlob.ConnectionString);
                    if (!client.GetBlobContainerClient(storage.AzureBlob.PublicContainer).GetBlobClient("validation").CanGenerateSasUri
                        || !IsPublicHttpsUrl(client.Uri.ToString()))
                    {
                        errors.Add("Storage:AzureBlob:ConnectionString must support HTTPS and shared-key SAS uploads; development storage is not allowed.");
                    }
                }
                catch (Exception ex) when (ex is ArgumentException or FormatException)
                {
                    errors.Add("Storage:AzureBlob:ConnectionString is invalid for the selected AzureBlob provider.");
                }
            }

            // The adapter can use the blob endpoint when a CDN/public override is omitted.
            if (!string.IsNullOrEmpty(storage.AzureBlob.PublicBaseUrl))
            {
                RequireHttpsUrl(storage.AzureBlob.PublicBaseUrl, "Storage:AzureBlob:PublicBaseUrl");
            }
        }
        else if (string.Equals(storage.Provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            if (builder.Environment.IsProduction())
            {
                errors.Add("Storage:Provider=Local is not supported in Production; use AzureBlob for durable shared storage.");
            }
            else
            {
                RequireValue(storage.Local.RootPath, "Storage:Local:RootPath");
                RequireHttpsUrl(storage.Local.PublicBaseUrl, "Storage:Local:PublicBaseUrl");
                RequireHttpsUrl(storage.Local.ApiBaseUrl, "Storage:Local:ApiBaseUrl");
                RequireValue(storage.Local.UploadSigningKey, "Storage:Local:UploadSigningKey");
                if (storage.Local.UploadSigningKey == new LocalStorageOptions().UploadSigningKey)
                {
                    errors.Add("Storage:Local:UploadSigningKey must not use the development key in Staging.");
                }
            }
        }
        else
        {
            errors.Add("Storage:Provider must be AzureBlob or Local; unknown providers fall back to unsafe local storage.");
        }

        var notifications = configuration.GetSection(NotificationsOptions.SectionName).Get<NotificationsOptions>()
            ?? new NotificationsOptions();
        RequireValue(notifications.Resend.ApiKey, "Notifications:Resend:ApiKey");
        RequireValue(notifications.Twilio.AccountSid, "Notifications:Twilio:AccountSid");
        RequireValue(notifications.Twilio.AuthToken, "Notifications:Twilio:AuthToken");

        // Global senders cover tenants whose market has no explicit override.
        RequireValue(notifications.Resend.From, "Notifications:Resend:From");
        RequireValue(notifications.Twilio.From, "Notifications:Twilio:From");
        for (var index = 0; index < notifications.Senders.Count; index++)
        {
            var sender = notifications.Senders[index];
            RequireValue(sender.FromEmail ?? notifications.Resend.From, $"Notifications:Senders:{index}:FromEmail");
            RequireValue(sender.SmsFrom ?? notifications.Twilio.From, $"Notifications:Senders:{index}:SmsFrom");
        }

        void RequireValue(string? value, string key)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"{key} must be set in Staging/Production.");
            }
        }

        void RequireHttpsUrl(string? value, string key)
        {
            if (!IsPublicHttpsUrl(value))
            {
                errors.Add($"{key} must be an absolute public HTTPS URL in Staging/Production.");
            }
        }

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

    private static bool IsPublicHttpsUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps && !uri.IsLoopback
        && !string.IsNullOrWhiteSpace(uri.Host) && !uri.Host.Contains('*')
        && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0 && uri.Query.Length == 0;
}
