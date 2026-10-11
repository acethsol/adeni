namespace Adeni.Infrastructure.Tests.Payments;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Payments;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class StripePaymentProviderTests
{
    [Fact]
    public async Task ParseWebhookAsync_rejects_missing_signature_when_secret_configured()
    {
        var provider = CreateProvider(new StripeOptions { WebhookSecret = "whsec_test_secret" });
        var result = await provider.ParseWebhookAsync("{}", new Dictionary<string, string>());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.PaymentWebhookInvalid, result.Error.Code);
    }

    [Fact]
    public async Task ParseWebhookAsync_rejects_missing_secret_in_staging()
    {
        var provider = CreateProvider(new StripeOptions(), Environments.Staging);
        var result = await provider.ParseWebhookAsync("{}", new Dictionary<string, string>());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.PaymentWebhookInvalid, result.Error.Code);
    }

    [Fact]
    public async Task ParseWebhookAsync_rejects_invalid_signature()
    {
        var options = new StripeOptions
        {
            WebhookSecret = "whsec_test_secret",
            WebhookToleranceSeconds = 0,
        };
        var provider = CreateProvider(options);
        var body = """{"type":"checkout.session.completed","data":{"object":{"id":"cs_test_123"}}}""";
        var headers = new Dictionary<string, string>
        {
            ["Stripe-Signature"] = $"t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()},v1=deadbeef",
        };

        var result = await provider.ParseWebhookAsync(body, headers);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.PaymentWebhookInvalid, result.Error.Code);
    }

    [Fact]
    public async Task ParseWebhookAsync_parses_checkout_session_completed()
    {
        var secret = "whsec_test_secret";
        var body = """
            {
              "type": "checkout.session.completed",
              "data": {
                "object": {
                  "id": "cs_test_abc123",
                  "payment_status": "paid",
                  "amount_total": 12500,
                  "currency": "cad",
                  "client_reference_id": "stripe_ref_local"
                }
              }
            }
            """;
        var options = new StripeOptions
        {
            WebhookSecret = secret,
            WebhookToleranceSeconds = 0,
        };
        var provider = CreateProvider(options);
        var headers = new Dictionary<string, string>
        {
            ["Stripe-Signature"] = BuildStripeSignature(body, secret),
        };

        var result = await provider.ParseWebhookAsync(body, headers);

        Assert.True(result.IsSuccess);
        Assert.Equal("cs_test_abc123", result.Value!.ProviderReference);
        Assert.True(result.Value.IsSuccessful);
        Assert.False(result.Value.IsFailed);
        Assert.Equal(125m, result.Value.Amount);
        Assert.Equal("CAD", result.Value.Currency);
    }

    [Fact]
    public void TryValidateWebhookSignature_accepts_valid_v1()
    {
        var secret = "whsec_test_secret";
        var body = "{\"id\":\"evt_1\"}";
        var options = new StripeOptions
        {
            WebhookSecret = secret,
            WebhookToleranceSeconds = 0,
        };
        var headers = new Dictionary<string, string>
        {
            ["Stripe-Signature"] = BuildStripeSignature(body, secret),
        };

        Assert.True(StripePaymentProvider.TryValidateWebhookSignature(body, headers, options));
    }

    private static StripePaymentProvider CreateProvider(
        StripeOptions options,
        string environmentName = "Development")
    {
        return new StripePaymentProvider(
            new HttpClient(),
            Options.Create(options),
            new TestHostEnvironment { EnvironmentName = environmentName },
            NullLogger<StripePaymentProvider>.Instance);
    }

    private static string BuildStripeSignature(string body, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signedPayload = $"{timestamp}.{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var signature = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={timestamp},v1={signature}";
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Adeni.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
