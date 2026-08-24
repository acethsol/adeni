namespace Adeni.Infrastructure.Payments;

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Adeni.Application.Payments;
using Adeni.Domain.Common;
using Adeni.Domain.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class PaystackPaymentProvider(
    HttpClient httpClient,
    IOptions<PaystackOptions> paystackOptions,
    ILogger<PaystackPaymentProvider> logger) : IPaymentProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public string ProviderName => "Paystack";

    public async Task<Result<ProviderInitializeResult>> InitializeAsync(
        ProviderInitializeRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = paystackOptions.Value;
        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            logger.LogError("Paystack secret key is not configured.");
            return Result.Failure<ProviderInitializeResult>(ErrorCodes.PaymentProviderFailedError());
        }

        var payload = new Dictionary<string, object?>
        {
            ["email"] = string.IsNullOrWhiteSpace(request.CustomerEmail)
                ? $"payments+{request.PaymentIntentId:N}@adeni.local"
                : request.CustomerEmail.Trim(),
            ["amount"] = ToMinorUnits(request.Amount, request.Currency),
            ["currency"] = request.Currency.ToUpperInvariant(),
            ["reference"] = request.ProviderReference,
            ["callback_url"] = request.CallbackUrl,
            ["metadata"] = BuildMetadata(request),
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await SendAsync(HttpMethod.Post, "/transaction/initialize", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Paystack initialize failed with status {StatusCode}", response.StatusCode);
            return Result.Failure<ProviderInitializeResult>(ErrorCodes.PaymentProviderFailedError());
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.GetProperty("status").GetBoolean())
            {
                return Result.Failure<ProviderInitializeResult>(ErrorCodes.PaymentProviderFailedError());
            }

            var data = document.RootElement.GetProperty("data");
            var authorizationUrl = data.GetProperty("authorization_url").GetString()
                ?? string.Empty;
            var reference = data.GetProperty("reference").GetString() ?? request.ProviderReference;

            return Result.Success(new ProviderInitializeResult(authorizationUrl, reference));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            logger.LogError(ex, "Failed to parse Paystack initialize response.");
            return Result.Failure<ProviderInitializeResult>(ErrorCodes.PaymentProviderFailedError());
        }
    }

    public async Task<Result<ProviderPaymentStatus>> GetProviderStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"/transaction/verify/{Uri.EscapeDataString(providerReference)}",
            content: null,
            cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return Result.Failure<ProviderPaymentStatus>(ErrorCodes.PaymentProviderFailedError());
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.GetProperty("status").GetBoolean())
            {
                return Result.Failure<ProviderPaymentStatus>(ErrorCodes.PaymentProviderFailedError());
            }

            var data = document.RootElement.GetProperty("data");
            var status = data.GetProperty("status").GetString() ?? string.Empty;
            var reference = data.GetProperty("reference").GetString() ?? providerReference;
            var gatewayResponse = data.TryGetProperty("gateway_response", out var gatewayProp)
                ? gatewayProp.GetString()
                : null;

            return Result.Success(new ProviderPaymentStatus(
                reference,
                string.Equals(status, "success", StringComparison.OrdinalIgnoreCase),
                string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase),
                gatewayResponse));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            logger.LogError(ex, "Failed to parse Paystack verify response.");
            return Result.Failure<ProviderPaymentStatus>(ErrorCodes.PaymentProviderFailedError());
        }
    }

    public Task<Result<PaymentWebhookPayload>> ParseWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        var options = paystackOptions.Value;
        if (!string.IsNullOrWhiteSpace(options.WebhookSecret))
        {
            if (!headers.TryGetValue("x-paystack-signature", out var signature)
                || string.IsNullOrWhiteSpace(signature))
            {
                return Task.FromResult(Result.Failure<PaymentWebhookPayload>(ErrorCodes.PaymentWebhookInvalidError()));
            }

            var computed = ComputeHmacSha512(rawBody, options.WebhookSecret);
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(computed),
                    Encoding.UTF8.GetBytes(signature)))
            {
                return Task.FromResult(Result.Failure<PaymentWebhookPayload>(ErrorCodes.PaymentWebhookInvalidError()));
            }
        }

        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(rawBody) ? "{}" : rawBody);
            var root = document.RootElement;
            var eventType = root.TryGetProperty("event", out var eventProp)
                ? eventProp.GetString() ?? "unknown"
                : "unknown";
            var data = root.TryGetProperty("data", out var dataProp) ? dataProp : default;

            var reference = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("reference", out var refProp)
                ? refProp.GetString() ?? string.Empty
                : string.Empty;

            var status = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString() ?? string.Empty
                : string.Empty;

            decimal? amount = null;
            if (data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("amount", out var amountProp)
                && amountProp.TryGetDecimal(out var minorAmount))
            {
                amount = minorAmount / 100m;
            }

            string? currency = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("currency", out var currencyProp)
                ? currencyProp.GetString()
                : null;

            var isSuccessful = string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
                || eventType.Contains("charge.success", StringComparison.OrdinalIgnoreCase);
            var isFailed = string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase)
                || eventType.Contains("charge.failed", StringComparison.OrdinalIgnoreCase);
            var isRefund = eventType.Contains("refund", StringComparison.OrdinalIgnoreCase);

            var failureReason = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("gateway_response", out var gatewayProp)
                ? gatewayProp.GetString()
                : null;

            return Task.FromResult(Result.Success(new PaymentWebhookPayload(
                eventType,
                reference,
                isSuccessful,
                isFailed,
                isRefund,
                failureReason,
                amount,
                currency)));
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid Paystack webhook payload.");
            return Task.FromResult(Result.Failure<PaymentWebhookPayload>(ErrorCodes.PaymentWebhookInvalidError()));
        }
    }

    public async Task<Result<ProviderRefundResult>> RefundAsync(
        string providerReference,
        decimal? amount,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?> { ["transaction"] = providerReference };
        if (amount.HasValue)
        {
            payload["amount"] = ToMinorUnits(amount.Value, "NGN");
        }

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await SendAsync(HttpMethod.Post, "/refund", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Paystack refund failed with status {StatusCode}", response.StatusCode);
            return Result.Failure<ProviderRefundResult>(ErrorCodes.PaymentProviderFailedError());
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (!document.RootElement.GetProperty("status").GetBoolean())
            {
                return Result.Failure<ProviderRefundResult>(ErrorCodes.PaymentProviderFailedError());
            }

            var data = document.RootElement.GetProperty("data");
            var status = data.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString() ?? "pending"
                : "pending";
            var refundedAmount = data.TryGetProperty("amount", out var amountProp) && amountProp.TryGetDecimal(out var minor)
                ? minor / 100m
                : amount ?? 0m;

            return Result.Success(new ProviderRefundResult(providerReference, refundedAmount, status));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            logger.LogError(ex, "Failed to parse Paystack refund response.");
            return Result.Failure<ProviderRefundResult>(ErrorCodes.PaymentProviderFailedError());
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var options = paystackOptions.Value;
        using var request = new HttpRequestMessage(method, new Uri(new Uri(options.BaseUrl.TrimEnd('/') + "/"), path.TrimStart('/')));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.SecretKey);
        if (content is not null)
        {
            request.Content = content;
        }

        return await httpClient.SendAsync(request, cancellationToken);
    }

    private static Dictionary<string, string> BuildMetadata(ProviderInitializeRequest request)
    {
        var metadata = new Dictionary<string, string>
        {
            ["payment_intent_id"] = request.PaymentIntentId.ToString(),
            ["tenant_id"] = request.TenantId.ToString(),
            ["type"] = request.Type.ToString(),
            ["platform_fee"] = request.PlatformFeeAmount.ToString("F2"),
        };

        if (request.BookingId.HasValue)
        {
            metadata["booking_id"] = request.BookingId.Value.ToString();
        }

        if (request.Metadata is not null)
        {
            foreach (var pair in request.Metadata)
            {
                metadata[pair.Key] = pair.Value;
            }
        }

        return metadata;
    }

    private static long ToMinorUnits(decimal amount, string currency) =>
        currency.Equals("NGN", StringComparison.OrdinalIgnoreCase)
            ? (long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero)
            : (long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    private static string ComputeHmacSha512(string payload, string secret)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
