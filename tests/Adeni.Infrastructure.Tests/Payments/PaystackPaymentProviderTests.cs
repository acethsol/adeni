namespace Adeni.Infrastructure.Tests.Payments;

using System.Text.Json;
using Adeni.Application.Payments;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Payments;
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

    private static PaystackPaymentProvider CreateProvider(PaystackOptions options)
    {
        return new PaystackPaymentProvider(
            new HttpClient(),
            Options.Create(options),
            NullLogger<PaystackPaymentProvider>.Instance);
    }
}
