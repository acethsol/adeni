namespace Adeni.Api.Tests.Extensions;

using Adeni.Api.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

public sealed class ProductionConfigurationExtensionsTests
{
    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void ValidateProductionConfiguration_ValidConfiguration_Accepts(string environment)
    {
        Assert.NotNull(CreateBuilder(environment).ValidateProductionConfiguration());
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void ValidateProductionConfiguration_LocalEnvironment_AllowsStubAndMissingNotifications(string environment)
    {
        var builder = CreateBuilder(environment);
        builder.Configuration["ConnectionStrings:AdeniDb"] = null;
        builder.Configuration["Storage:Provider"] = "Local";
        builder.Configuration["Payments:Provider"] = "Stub";
        builder.Configuration["SubscriptionBilling:Provider"] = "Stub";
        builder.Configuration["Notifications:Resend:ApiKey"] = null;
        builder.Configuration["Notifications:Twilio:AuthToken"] = null;

        Assert.Same(builder, builder.ValidateProductionConfiguration());
    }

    [Theory]
    [InlineData("Payments:Provider", "Stub")]
    [InlineData("Payments:Provider", "sTuB")]
    [InlineData("Payments:Provider", null)]
    [InlineData("Payments:Provider", "Typo")]
    [InlineData("Finance:Provider", "Stub")]
    [InlineData("SubscriptionBilling:Provider", "Stub")]
    [InlineData("SubscriptionBilling:Provider", "sTuB")]
    [InlineData("SubscriptionBilling:Provider", null)]
    [InlineData("SubscriptionBilling:Provider", "Typo")]
    public void ValidateProductionConfiguration_StubOrFallbackProvider_Rejects(string key, string? value)
    {
        foreach (var environment in new[] { "Staging", "Production" })
        {
            var builder = CreateBuilder(environment);
            builder.Configuration[key] = value;

            var error = Assert.Throws<InvalidOperationException>(() => builder.ValidateProductionConfiguration());

            Assert.Contains(key, error.Message);
            Assert.Contains("Stub is not allowed", error.Message);
        }
    }

    [Theory]
    [InlineData("Notifications:Resend:ApiKey")]
    [InlineData("Notifications:Resend:From")]
    [InlineData("Notifications:Twilio:AccountSid")]
    [InlineData("Notifications:Twilio:AuthToken")]
    [InlineData("Notifications:Twilio:From")]
    [InlineData("Notifications:Senders:0:FromEmail")]
    [InlineData("Notifications:Senders:0:SmsFrom")]
    public void ValidateProductionConfiguration_MissingNotificationValue_RejectsClearly(string key)
    {
        foreach (var environment in new[] { "Staging", "Production" })
        foreach (var value in new string?[] { null, "", " " })
        {
            // Null market overrides inherit the valid global sender.
            if (key.Contains(":Senders:") && value is null)
            {
                continue;
            }

            var builder = CreateBuilder(environment);
            builder.Configuration[key] = value;

            var error = Assert.Throws<InvalidOperationException>(() => builder.ValidateProductionConfiguration());

            Assert.Contains(key, error.Message);
            Assert.Contains("Production configuration validation failed", error.Message);
        }
    }

    [Theory]
    [InlineData("Paystack", "Paystack")]
    [InlineData("Stripe", "MarketDefault")]
    [InlineData("marketdefault", "paystack")]
    public void ValidateProductionConfiguration_RealProvidersAndSenderFallback_Accepts(string payments, string subscriptions)
    {
        var builder = CreateBuilder("Production");
        builder.Configuration["Payments:Provider"] = payments;
        builder.Configuration["SubscriptionBilling:Provider"] = subscriptions;
        builder.Configuration["Notifications:Senders:0:CountryCode"] = "NG";
        builder.Configuration["Notifications:Senders:1:CountryCode"] = "CA";
        builder.Configuration["Notifications:Senders:1:FromEmail"] = "ottawa@example.com";
        builder.Configuration["Notifications:Senders:1:SmsFrom"] = "+16135550100";

        Assert.Same(builder, builder.ValidateProductionConfiguration());
    }

    [Fact]
    public void ValidateProductionConfiguration_FinanceOverride_UsesEffectiveProvider()
    {
        var builder = CreateBuilder("Production");
        builder.Configuration["Payments:Provider"] = "Stub";
        builder.Configuration["Finance:Provider"] = "Stripe";

        Assert.Same(builder, builder.ValidateProductionConfiguration());
    }

    [Theory]
    [InlineData("ConnectionStrings:AdeniDb", null)]
    [InlineData("ConnectionStrings:AdeniDb", "not-sql")]
    [InlineData("ConnectionStrings:AdeniDb", "Database=adeni")]
    [InlineData("Auth0:Domain", "")]
    [InlineData("Auth0:Domain", "https://tenant.auth0.com")]
    [InlineData("Auth0:Audience", " ")]
    [InlineData("Cors:AllowedOrigins:0", "*")]
    [InlineData("Cors:AllowedOrigins:0", "https://app.example.com/path")]
    [InlineData("Cors:AllowedOrigins:0", "https://app.example.com/")]
    [InlineData("Cors:AllowedOrigins:0", "http://localhost:5173")]
    [InlineData("Paystack:SecretKey", "")]
    [InlineData("Paystack:BaseUrl", "http://api.paystack.co")]
    [InlineData("Paystack:WebhookSecret", "")]
    [InlineData("SubscriptionBilling:Paystack:BaseUrl", "invalid")]
    [InlineData("Storage:Provider", "Typo")]
    [InlineData("Storage:AzureBlob:ConnectionString", "")]
    [InlineData("Storage:AzureBlob:ConnectionString", "invalid")]
    [InlineData("Storage:AzureBlob:ConnectionString", "UseDevelopmentStorage=true")]
    [InlineData("Storage:AzureBlob:PublicContainer", "")]
    [InlineData("Storage:AzureBlob:PublicContainer", "INVALID")]
    [InlineData("Storage:AzureBlob:ConnectionString", "BlobEndpoint=https://adeni.blob.core.windows.net;SharedAccessSignature=sv=test")]
    [InlineData("App:PublicWebBaseUrl", "https://app.example.com/?query=value")]
    [InlineData("Storage:AzureBlob:PublicBaseUrl", "http://localhost/media")]
    [InlineData("App:PublicWebBaseUrl", "http://localhost:5190")]
    [InlineData("Portal:PublicBaseUrl", null)]
    public void ValidateProductionConfiguration_InvalidRequiredSetting_Rejects(string key, string? value)
    {
        foreach (var environment in new[] { "Staging", "Production" })
        {
            var builder = CreateBuilder(environment);
            builder.Configuration[key] = value;
            var error = Assert.Throws<InvalidOperationException>(() => builder.ValidateProductionConfiguration());
            Assert.Contains(key.Split(':')[0], error.Message);
        }
    }

    [Fact]
    public void ValidateProductionConfiguration_OnlySelectedProviders_RequiresTheirSettings()
    {
        var builder = CreateBuilder("Production");
        builder.Configuration["Payments:Provider"] = "Stripe";
        builder.Configuration["Paystack:SecretKey"] = null;
        builder.Configuration["Paystack:WebhookSecret"] = null;
        builder.Configuration["SubscriptionBilling:Paystack:SecretKey"] = "billing-key";
        builder.Configuration["SubscriptionBilling:Paystack:WebhookSecret"] = "billing-webhook";
        builder.Configuration["Paystack:BaseUrl"] = "unused";
        // Public/publishable keys, plan codes, and CDN overrides are optional.
        Assert.Same(builder, builder.ValidateProductionConfiguration());

        builder.Configuration["Stripe:SecretKey"] = null;
        Assert.Contains("Stripe:SecretKey", Assert.Throws<InvalidOperationException>(() => builder.ValidateProductionConfiguration()).Message);
    }

    [Fact]
    public void ValidateProductionConfiguration_LocalStorage_RejectsProductionAndUnsafeStaging()
    {
        var builder = CreateBuilder("Production");
        builder.Configuration["Storage:Provider"] = "Local";
        Assert.Contains("Storage:Provider=Local", Assert.Throws<InvalidOperationException>(() => builder.ValidateProductionConfiguration()).Message);

        builder = CreateBuilder("Staging");
        builder.Configuration["Storage:Provider"] = "Local";
        Assert.Contains("Storage:Local", Assert.Throws<InvalidOperationException>(() => builder.ValidateProductionConfiguration()).Message);
        builder.Configuration["Storage:Local:ApiBaseUrl"] = "https://api.example.com";
        builder.Configuration["Storage:Local:PublicBaseUrl"] = "https://api.example.com/media";
        builder.Configuration["Storage:Local:UploadSigningKey"] = "staging-secret";
        Assert.Same(builder, builder.ValidateProductionConfiguration());
    }

    [Theory]
    [InlineData("$root")]
    [InlineData("$web")]
    public void ValidateProductionConfiguration_AzureSpecialContainer_Accepts(string container)
    {
        var builder = CreateBuilder("Production");
        builder.Configuration["Storage:AzureBlob:PublicContainer"] = container;
        Assert.Same(builder, builder.ValidateProductionConfiguration());
    }

    private static WebApplicationBuilder CreateBuilder(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AdeniDb"] = "Server=sql.example.com;Database=adeni;Integrated Security=true",
            ["Auth0:Domain"] = "tenant.auth0.com",
            ["Auth0:Audience"] = "https://api.example.com",
            ["App:PublicWebBaseUrl"] = "https://app.example.com",
            ["Portal:PublicBaseUrl"] = "https://portal.example.com",
            ["Storage:Provider"] = "AzureBlob",
            ["Storage:AzureBlob:ConnectionString"] = "DefaultEndpointsProtocol=https;AccountName=adeni;AccountKey=" + Convert.ToBase64String(new byte[32]) + ";EndpointSuffix=core.windows.net",
            ["Paystack:SecretKey"] = "test-key",
            ["Stripe:SecretKey"] = "test-key",
            ["Auth0:Enabled"] = "true",
            ["Cors:AllowedOrigins:0"] = "https://app.example.com",
            ["Payments:Provider"] = "Paystack",
            ["Paystack:WebhookSecret"] = "test-paystack-secret",
            ["Stripe:WebhookSecret"] = "test-stripe-secret",
            ["SubscriptionBilling:Provider"] = "Paystack",
            ["Notifications:Resend:ApiKey"] = "test-key",
            ["Notifications:Resend:From"] = "sender@example.com",
            ["Notifications:Twilio:AccountSid"] = "test-sid",
            ["Notifications:Twilio:AuthToken"] = "test-token",
            ["Notifications:Twilio:From"] = "+15555550100",
        });
        return builder;
    }
}
