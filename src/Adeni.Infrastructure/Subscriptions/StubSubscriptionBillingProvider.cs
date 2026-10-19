namespace Adeni.Infrastructure.Subscriptions;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class StubSubscriptionBillingProvider(
    IOptions<SubscriptionBillingOptions> billingOptions,
    IHostEnvironment environment,
    ILogger<StubSubscriptionBillingProvider> logger)
    : ISubscriptionBillingProvider
{
    public Task<Result<SubscriptionCheckoutResponse>> CreateCheckoutAsync(
        CreateSubscriptionCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var skuApi = SubscriptionSkuMapping.ToApiValue(request.TargetSku);
        logger.LogInformation(
            "Stub subscription checkout for tenant {TenantId} → sku {Sku}",
            request.TenantId,
            skuApi);

        var reference = $"stub_sub_{request.TenantId:N}_{skuApi}";
        var checkoutUrl =
            $"{request.SuccessUrl}?stub_checkout=1&reference={Uri.EscapeDataString(reference)}&sku={Uri.EscapeDataString(skuApi)}";

        return Task.FromResult(Result.Success(new SubscriptionCheckoutResponse(
            checkoutUrl,
            reference,
            "pending")));
    }

    public Task<Result<SubscriptionWebhookEvent>> ParseWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        var secret = billingOptions.Value.Paystack.WebhookSecret;
        if (!string.IsNullOrWhiteSpace(secret))
        {
            if (!TryValidateWebhookSignature(rawBody, headers, secret))
            {
                return Task.FromResult(Result.Failure<SubscriptionWebhookEvent>(
                    ErrorCodes.PaymentWebhookInvalidError()));
            }
        }
        else if (!environment.IsDevelopment() && environment.EnvironmentName != "Testing")
        {
            return Task.FromResult(Result.Failure<SubscriptionWebhookEvent>(
                ErrorCodes.PaymentWebhookInvalidError()));
        }

        logger.LogInformation("Stub subscription webhook received ({Length} bytes)", rawBody.Length);

        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(rawBody) ? "{}" : rawBody);
            var root = document.RootElement;

            var eventType = root.TryGetProperty("event", out var eventProp)
                ? eventProp.GetString() ?? "subscription.unknown"
                : "subscription.unknown";

            var reference = root.TryGetProperty("reference", out var refProp)
                ? refProp.GetString() ?? string.Empty
                : string.Empty;

            string? sku = null;
            if (root.TryGetProperty("sku", out var skuProp))
            {
                sku = skuProp.GetString();
            }
            else if (root.TryGetProperty("tier", out var tierProp))
            {
                sku = tierProp.GetString();
            }

            Guid? tenantId = null;
            if (root.TryGetProperty("tenantId", out var tenantProp)
                && Guid.TryParse(tenantProp.GetString(), out var parsedTenantId))
            {
                tenantId = parsedTenantId;
            }

            var status = root.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString() ?? "received"
                : "received";

            return Task.FromResult(Result.Success(new SubscriptionWebhookEvent(
                eventType,
                reference,
                tenantId,
                sku,
                status,
                DateTimeOffset.UtcNow)));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(Result.Failure<SubscriptionWebhookEvent>(
                Error.Validation($"Invalid subscription webhook payload: {ex.Message}")));
        }
    }

    private static bool TryValidateWebhookSignature(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        string webhookSecret)
    {
        if (!headers.TryGetValue("x-paystack-signature", out var signature)
            || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(webhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        var computed = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(signature));
    }
}
