namespace Adeni.Infrastructure.Subscriptions;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Subscriptions;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class PaystackSubscriptionBillingProvider(
    HttpClient httpClient,
    AdeniDbContext dbContext,
    IOptions<SubscriptionBillingOptions> billingOptions,
    IHostEnvironment environment,
    ILogger<PaystackSubscriptionBillingProvider> logger) : ISubscriptionBillingProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly HashSet<string> ActivationEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "subscription.create",
        "charge.success",
        "invoice.payment_succeeded",
        "invoice.update",
    };

    public async Task<Result<SubscriptionCheckoutResponse>> CreateCheckoutAsync(
        CreateSubscriptionCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var paystack = billingOptions.Value.Paystack;
        if (string.IsNullOrWhiteSpace(paystack.SecretKey))
        {
            logger.LogError("Paystack secret key is not configured for subscription billing.");
            return Result.Failure<SubscriptionCheckoutResponse>(ErrorCodes.PaymentProviderFailedError());
        }

        if (request.TargetTier is SubscriptionTier.Free)
        {
            return Result.Failure<SubscriptionCheckoutResponse>(
                Error.Validation("Choose a paid plan to upgrade."));
        }

        var tierApi = SubscriptionTierMapping.ToApiValue(request.TargetTier);
        var reference = $"adeni_sub_{request.TenantId:N}_{tierApi}_{Guid.NewGuid():N}"[..64];
        var (country, currency) = await ResolveTenantCountryAsync(request.TenantId, cancellationToken);
        var sku = SubscriptionSkuMapping.FromLegacyTier(request.TargetTier);
        var planCode = ResolvePlanCode(sku, paystack);

        var payload = new Dictionary<string, object?>
        {
            ["email"] = request.CustomerEmail.Trim(),
            ["reference"] = reference,
            ["callback_url"] = request.SuccessUrl,
            ["metadata"] = new Dictionary<string, string>
            {
                ["tenant_id"] = request.TenantId.ToString(),
                ["target_tier"] = tierApi,
                ["billing_type"] = "saas_subscription",
            },
        };

        if (!string.IsNullOrWhiteSpace(planCode))
        {
            payload["plan"] = planCode;
        }
        else
        {
            var minor = SubscriptionCatalogPrices.ToAmountMinor(country, sku);
            if (minor is null or <= 0)
            {
                return Result.Failure<SubscriptionCheckoutResponse>(
                    Error.Validation("Subscription price is not configured for this market."));
            }

            payload["amount"] = minor.Value;
            payload["currency"] = currency;
        }

        using var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await SendAsync(HttpMethod.Post, "/transaction/initialize", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Paystack subscription initialize failed with status {StatusCode}", response.StatusCode);
            return Result.Failure<SubscriptionCheckoutResponse>(ErrorCodes.PaymentProviderFailedError());
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.GetProperty("status").GetBoolean())
            {
                return Result.Failure<SubscriptionCheckoutResponse>(ErrorCodes.PaymentProviderFailedError());
            }

            var data = document.RootElement.GetProperty("data");
            var authorizationUrl = data.GetProperty("authorization_url").GetString() ?? string.Empty;
            var providerReference = data.GetProperty("reference").GetString() ?? reference;

            return Result.Success(new SubscriptionCheckoutResponse(
                authorizationUrl,
                providerReference,
                "pending"));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            logger.LogError(ex, "Failed to parse Paystack subscription initialize response.");
            return Result.Failure<SubscriptionCheckoutResponse>(ErrorCodes.PaymentProviderFailedError());
        }
    }

    public Task<Result<SubscriptionWebhookEvent>> ParseWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        var paystack = billingOptions.Value.Paystack;
        var isDevOrTesting = environment.IsDevelopment() || environment.EnvironmentName == "Testing";

        if (!isDevOrTesting)
        {
            if (string.IsNullOrWhiteSpace(paystack.WebhookSecret))
            {
                logger.LogError("Paystack webhook secret is not configured.");
                return Task.FromResult(Result.Failure<SubscriptionWebhookEvent>(ErrorCodes.PaymentWebhookInvalidError()));
            }

            if (!PaystackSubscriptionWebhookSupport.TryValidateSignature(rawBody, headers, paystack.WebhookSecret))
            {
                return Task.FromResult(Result.Failure<SubscriptionWebhookEvent>(ErrorCodes.PaymentWebhookInvalidError()));
            }
        }
        else if (!string.IsNullOrWhiteSpace(paystack.WebhookSecret)
                 && !PaystackSubscriptionWebhookSupport.TryValidateSignature(rawBody, headers, paystack.WebhookSecret))
        {
            return Task.FromResult(Result.Failure<SubscriptionWebhookEvent>(ErrorCodes.PaymentWebhookInvalidError()));
        }

        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(rawBody) ? "{}" : rawBody);
            var root = document.RootElement;

            if (!root.TryGetProperty("event", out var eventProp))
            {
                return Task.FromResult(Result.Failure<SubscriptionWebhookEvent>(
                    Error.Validation("Paystack webhook missing event.")));
            }

            var eventType = NormalizeEventType(eventProp.GetString() ?? "unknown");
            var data = PaystackSubscriptionWebhookSupport.GetDataObject(root);
            var metadata = PaystackSubscriptionWebhookSupport.GetMetadata(data);

            var tenantId = PaystackSubscriptionWebhookSupport.TryGetMetadataTenantId(metadata);
            var tier = PaystackSubscriptionWebhookSupport.TryGetMetadataTier(metadata)
                ?? MapPlanCodeToTier(data);

            var reference = ResolveProviderReference(data, eventType);
            var status = ResolveStatus(data, eventType);
            var (amountMinor, currency) = ResolveAmount(data);

            if (tenantId is null && eventType.Contains("subscription", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Paystack subscription webhook missing tenant_id metadata.");
            }

            return Task.FromResult(Result.Success(new SubscriptionWebhookEvent(
                eventType,
                reference,
                tenantId,
                tier,
                status,
                DateTimeOffset.UtcNow,
                amountMinor,
                currency)));
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid Paystack subscription webhook payload.");
            return Task.FromResult(Result.Failure<SubscriptionWebhookEvent>(ErrorCodes.PaymentWebhookInvalidError()));
        }
    }

    private async Task<(string Country, string Currency)> ResolveTenantCountryAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var marketId = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsPrimary && x.IsActive)
            .Select(x => x.MarketId)
            .FirstOrDefaultAsync(cancellationToken);

        var country = await dbContext.CatalogMarkets
            .AsNoTracking()
            .Where(x => x.Id == marketId)
            .Select(x => x.CountryCode)
            .FirstOrDefaultAsync(cancellationToken);

        country = string.IsNullOrWhiteSpace(country)
            ? SubscriptionCatalogPrices.DefaultCountryCode
            : country;

        return (country, SubscriptionCatalogPrices.CurrencyForCountry(country));
    }

    private static string? ResolvePlanCode(SubscriptionSku sku, SubscriptionBillingPaystackOptions paystack) =>
        sku switch
        {
            SubscriptionSku.Professional => NullIfEmpty(paystack.ProfessionalPlanCode),
            SubscriptionSku.Business => NullIfEmpty(paystack.BusinessPlanCode),
            _ => null,
        };

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeEventType(string eventType)
    {
        if (ActivationEvents.Contains(eventType))
        {
            return eventType switch
            {
                "charge.success" => "subscription.create",
                _ => eventType,
            };
        }

        return eventType;
    }

    private static string? MapPlanCodeToTier(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (data.TryGetProperty("plan", out var plan) && plan.ValueKind == JsonValueKind.Object)
        {
            if (plan.TryGetProperty("plan_code", out var codeProp))
            {
                return MapPlanCodeString(codeProp.GetString());
            }
        }

        if (data.TryGetProperty("plan_code", out var directCode))
        {
            return MapPlanCodeString(directCode.GetString());
        }

        return null;
    }

    private static string? MapPlanCodeString(string? planCode)
    {
        if (string.IsNullOrWhiteSpace(planCode))
        {
            return null;
        }

        var normalized = planCode.ToLowerInvariant();
        if (normalized.Contains("business", StringComparison.Ordinal))
        {
            return "business";
        }

        if (normalized.Contains("pro", StringComparison.Ordinal)
            || normalized.Contains("professional", StringComparison.Ordinal))
        {
            return "pro";
        }

        return null;
    }

    private static string ResolveProviderReference(JsonElement data, string eventType)
    {
        if (data.ValueKind == JsonValueKind.Object)
        {
            if (data.TryGetProperty("subscription_code", out var subCode)
                && !string.IsNullOrWhiteSpace(subCode.GetString()))
            {
                return subCode.GetString()!;
            }

            if (data.TryGetProperty("reference", out var reference)
                && !string.IsNullOrWhiteSpace(reference.GetString()))
            {
                return reference.GetString()!;
            }

            if (data.TryGetProperty("id", out var idProp))
            {
                return idProp.ToString();
            }
        }

        return $"{eventType}:{Guid.NewGuid():N}";
    }

    private static string ResolveStatus(JsonElement data, string eventType)
    {
        if (eventType.Contains("payment_failed", StringComparison.OrdinalIgnoreCase))
        {
            return "past_due";
        }

        if (eventType.Contains("disable", StringComparison.OrdinalIgnoreCase)
            || eventType.Contains("not_renew", StringComparison.OrdinalIgnoreCase))
        {
            return "canceled";
        }

        if (data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("status", out var statusProp)
            && !string.IsNullOrWhiteSpace(statusProp.GetString()))
        {
            return statusProp.GetString()!;
        }

        return "active";
    }

    private static (long? AmountMinor, string? Currency) ResolveAmount(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("amount", out var amountProp)
            || !amountProp.TryGetInt64(out var amount))
        {
            return (null, null);
        }

        var currency = data.TryGetProperty("currency", out var currencyProp)
            ? currencyProp.GetString()
            : null;

        return (amount, currency);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var paystack = billingOptions.Value.Paystack;
        using var request = new HttpRequestMessage(method, new Uri(new Uri(paystack.BaseUrl.TrimEnd('/') + "/"), path.TrimStart('/')));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", paystack.SecretKey);
        if (content is not null)
        {
            request.Content = content;
        }

        return await httpClient.SendAsync(request, cancellationToken);
    }
}
