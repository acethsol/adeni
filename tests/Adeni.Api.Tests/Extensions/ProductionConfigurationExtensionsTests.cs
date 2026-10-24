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

    private static WebApplicationBuilder CreateBuilder(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
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
