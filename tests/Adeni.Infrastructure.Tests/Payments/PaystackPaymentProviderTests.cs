namespace Adeni.Infrastructure.Tests.Payments;

using System.Text.Json;
using Adeni.Application.Payments;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Payments;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public sealed class PaystackPaymentProviderTests
{
    [Fact]
    public async Task ParseWebhookAsync_rejects_missing_signature_when_secret_configured()
    {
        var provider = CreateProvider(new PaystackOptions { WebhookSecret = "secret-key" });
        var result = await provider.ParseWebhookAsync("{}", new Dictionary<string, string>());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.PaymentWebhookInvalid, result.Error.Code);
    }

    [Fact]
    public async Task ParseWebhookAsync_rejects_missing_signature_in_staging_when_secret_configured()
    {
        var provider = CreateProvider(
            new PaystackOptions { WebhookSecret = "secret-key" },
            Environments.Staging);

        var result = await provider.ParseWebhookAsync("{}", new Dictionary<string, string>());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.PaymentWebhookInvalid, result.Error.Code);
    }

    [Fact]
    public async Task ParseWebhookAsync_rejects_missing_secret_in_staging()
    {
        var provider = CreateProvider(new PaystackOptions(), Environments.Staging);

        var result = await provider.ParseWebhookAsync("{}", new Dictionary<string, string>());

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.PaymentWebhookInvalid, result.Error.Code);
    }

    [Fact]
    public async Task ParseWebhookAsync_parses_charge_success_payload()
    {
        var body = """
            {
              "event": "charge.success",
              "data": {
                "reference": "pay_ref_123",
                "status": "success",
                "amount": 500000,
                "currency": "NGN"
              }
            }
            """;

        var provider = CreateProvider(new PaystackOptions());
        var result = await provider.ParseWebhookAsync(body, new Dictionary<string, string>());

        Assert.True(result.IsSuccess);
        Assert.Equal("pay_ref_123", result.Value!.ProviderReference);
        Assert.True(result.Value.IsSuccessful);
        Assert.Equal(5000m, result.Value.Amount);
    }

    private static PaystackPaymentProvider CreateProvider(
        PaystackOptions options,
        string environmentName = "Development")
    {
        return new PaystackPaymentProvider(
            new HttpClient(),
            Options.Create(options),
            new TestHostEnvironment { EnvironmentName = environmentName },
            NullLogger<PaystackPaymentProvider>.Instance);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Adeni.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
