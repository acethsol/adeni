namespace Adeni.Infrastructure.Tests.Subscriptions;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Adeni.Application.Subscriptions;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

public sealed class PaystackSubscriptionBillingProviderTests
{
    private const string WebhookSecret = "test-subscription-webhook-secret";

    [Fact]
    public async Task ParseWebhookAsync_rejects_missing_signature_when_secret_configured()
    {
        var provider = CreateProvider();
        var result = await provider.ParseWebhookAsync("{}", new Dictionary<string, string>());
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ParseWebhookAsync_parses_subscription_create_with_metadata()
    {
        var provider = CreateProvider();
        var tenantId = Guid.NewGuid();
        var body = JsonSerializer.Serialize(new
        {
            @event = "subscription.create",
            data = new
            {
                subscription_code = "SUB_test123",
                status = "active",
                amount = 70_000,
                currency = "NGN",
                metadata = new
                {
                    tenant_id = tenantId.ToString(),
                    target_tier = "pro",
                },
            },
        });

        var headers = Sign(body);
        var result = await provider.ParseWebhookAsync(body, headers);

        Assert.True(result.IsSuccess);
        Assert.Equal("subscription.create", result.Value!.EventType);
        Assert.Equal("SUB_test123", result.Value.ProviderReference);
        Assert.Equal(tenantId, result.Value.TenantId);
        Assert.Equal("pro", result.Value.Tier);
        Assert.Equal(70_000, result.Value.AmountMinor);
    }

    private static PaystackSubscriptionBillingProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddSingleton<IHostEnvironment>(new TestingHostEnvironment());
        services.AddSingleton<IOptions<SubscriptionBillingOptions>>(Options.Create(new SubscriptionBillingOptions
        {
            Provider = "Paystack",
            Paystack = new SubscriptionBillingPaystackOptions
            {
                SecretKey = "sk_test",
                WebhookSecret = WebhookSecret,
            },
        }));
        services.AddHttpClient<PaystackSubscriptionBillingProvider>();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<PaystackSubscriptionBillingProvider>();
    }

    private static Dictionary<string, string> Sign(string body)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(WebhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();
        return new Dictionary<string, string> { ["x-paystack-signature"] = signature };
    }

    private sealed class TestingHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Adeni.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
