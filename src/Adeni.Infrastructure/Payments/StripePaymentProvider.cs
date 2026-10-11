namespace Adeni.Infrastructure.Payments;

using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Adeni.Application.Payments;
using Adeni.Domain.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class StripePaymentProvider(
    HttpClient httpClient,
    IOptions<StripeOptions> stripeOptions,
    IHostEnvironment environment,
    ILogger<StripePaymentProvider> logger) : IPaymentProvider
{
    public string ProviderName => "Stripe";

    public async Task<Result<ProviderInitializeResult>> InitializeAsync(
        ProviderInitializeRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = stripeOptions.Value;
        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            logger.LogError("Stripe secret key is not configured.");
            return Result.Failure<ProviderInitializeResult>(ErrorCodes.PaymentProviderFailedError());
        }

        if (request.Amount <= 0)
        {
            return Result.Failure<ProviderInitializeResult>(ErrorCodes.PaymentInvalidAmountError());
        }

        var currency = request.Currency.Trim().ToLowerInvariant();
        if (currency.Length != 3)
        {
            return Result.Failure<ProviderInitializeResult>(ErrorCodes.PaymentInvalidCurrencyError());
        }

        var successUrl = string.IsNullOrWhiteSpace(request.CallbackUrl)
            ? "https://adeni.local/checkout/receipt?session_id={CHECKOUT_SESSION_ID}"
            : AppendSessionPlaceholder(request.CallbackUrl.Trim());
        var cancelUrl = successUrl;

        var productName = string.IsNullOrWhiteSpace(request.Description)
            ? $"Adeni {request.Type}"
            : request.Description.Trim();

        var form = new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["success_url"] = successUrl,
            ["cancel_url"] = cancelUrl,
            ["client_reference_id"] = request.ProviderReference,
            ["line_items[0][quantity]"] = "1",
            ["line_items[0][price_data][currency]"] = currency,
            ["line_items[0][price_data][unit_amount]"] = ToMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture),
            ["line_items[0][price_data][product_data][name]"] = productName,
            ["metadata[payment_intent_id]"] = request.PaymentIntentId.ToString(),
            ["metadata[tenant_id]"] = request.TenantId.ToString(),
            ["metadata[type]"] = request.Type.ToString(),
            ["metadata[platform_fee]"] = request.PlatformFeeAmount.ToString("F2", CultureInfo.InvariantCulture),
            ["metadata[adeni_reference]"] = request.ProviderReference,
        };

        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            form["customer_email"] = request.CustomerEmail.Trim();
        }

        if (request.BookingId.HasValue)
        {
            form["metadata[booking_id]"] = request.BookingId.Value.ToString();
        }

        if (request.Metadata is not null)
        {
            foreach (var pair in request.Metadata)
            {
                form[$"metadata[{pair.Key}]"] = pair.Value;
            }
        }

        using var content = new FormUrlEncodedContent(form);
        using var response = await SendAsync(HttpMethod.Post, "/v1/checkout/sessions", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Stripe Checkout Session create failed with status {StatusCode}", response.StatusCode);
            return Result.Failure<ProviderInitializeResult>(ErrorCodes.PaymentProviderFailedError());
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var sessionId = root.GetProperty("id").GetString() ?? string.Empty;
            var checkoutUrl = root.TryGetProperty("url", out var urlProp)
                ? urlProp.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(checkoutUrl))
            {
                return Result.Failure<ProviderInitializeResult>(ErrorCodes.PaymentProviderFailedError());
            }

            // Persist Stripe session id so webhooks and refunds resolve by provider reference.
            return Result.Success(new ProviderInitializeResult(checkoutUrl, sessionId));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogError(ex, "Failed to parse Stripe Checkout Session response.");
            return Result.Failure<ProviderInitializeResult>(ErrorCodes.PaymentProviderFailedError());
        }
    }

    public async Task<Result<ProviderPaymentStatus>> GetProviderStatusAsync(
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"/v1/checkout/sessions/{Uri.EscapeDataString(providerReference)}",
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
            var root = document.RootElement;
            var status = root.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString() ?? string.Empty
                : string.Empty;
            var paymentStatus = root.TryGetProperty("payment_status", out var payStatusProp)
                ? payStatusProp.GetString() ?? string.Empty
                : string.Empty;

            var isSuccessful = string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "complete", StringComparison.OrdinalIgnoreCase);
            var isFailed = string.Equals(status, "expired", StringComparison.OrdinalIgnoreCase);

            return Result.Success(new ProviderPaymentStatus(
                providerReference,
                isSuccessful,
                isFailed,
                isFailed ? status : null));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            logger.LogError(ex, "Failed to parse Stripe Checkout Session status.");
            return Result.Failure<ProviderPaymentStatus>(ErrorCodes.PaymentProviderFailedError());
        }
    }

    public Task<Result<PaymentWebhookPayload>> ParseWebhookAsync(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var options = stripeOptions.Value;
        var isDevOrTesting = environment.IsDevelopment() || environment.EnvironmentName == "Testing";

        if (!isDevOrTesting)
        {
            if (string.IsNullOrWhiteSpace(options.WebhookSecret))
            {
                logger.LogError("Stripe webhook secret is not configured.");
                return Task.FromResult(Result.Failure<PaymentWebhookPayload>(ErrorCodes.PaymentWebhookInvalidError()));
            }

            if (!TryValidateWebhookSignature(rawBody, headers, options))
            {
                return Task.FromResult(Result.Failure<PaymentWebhookPayload>(ErrorCodes.PaymentWebhookInvalidError()));
            }
        }
        else if (!string.IsNullOrWhiteSpace(options.WebhookSecret)
            && !TryValidateWebhookSignature(rawBody, headers, options))
        {
            return Task.FromResult(Result.Failure<PaymentWebhookPayload>(ErrorCodes.PaymentWebhookInvalidError()));
        }

        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(rawBody) ? "{}" : rawBody);
            var root = document.RootElement;
            var eventType = root.TryGetProperty("type", out var typeProp)
                ? typeProp.GetString() ?? "unknown"
                : "unknown";
            var dataObject = root.TryGetProperty("data", out var dataProp)
                && dataProp.TryGetProperty("object", out var objectProp)
                    ? objectProp
                    : default;

            var reference = ResolveWebhookReference(dataObject, eventType);
            var isSuccessful = eventType is "checkout.session.completed" or "checkout.session.async_payment_succeeded"
                || eventType is "payment_intent.succeeded";
            var isFailed = eventType is "checkout.session.expired" or "checkout.session.async_payment_failed"
                || eventType is "payment_intent.payment_failed";
            var isRefund = eventType.StartsWith("charge.refund", StringComparison.OrdinalIgnoreCase)
                || eventType.Equals("refund.created", StringComparison.OrdinalIgnoreCase)
                || eventType.Equals("refund.updated", StringComparison.OrdinalIgnoreCase);

            decimal? amount = null;
            string? currency = null;
            if (dataObject.ValueKind == JsonValueKind.Object)
            {
                if (dataObject.TryGetProperty("amount_total", out var amountTotal)
                    && amountTotal.TryGetInt64(out var minorTotal))
                {
                    amount = minorTotal / 100m;
                }
                else if (dataObject.TryGetProperty("amount", out var amountProp)
                    && amountProp.TryGetInt64(out var minorAmount))
                {
                    amount = minorAmount / 100m;
                }

                if (dataObject.TryGetProperty("currency", out var currencyProp))
                {
                    currency = currencyProp.GetString()?.ToUpperInvariant();
                }
            }

            return Task.FromResult(Result.Success(new PaymentWebhookPayload(
                eventType,
                reference,
                isSuccessful,
                isFailed,
                isRefund,
                null,
                amount,
                currency)));
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid Stripe webhook payload.");
            return Task.FromResult(Result.Failure<PaymentWebhookPayload>(ErrorCodes.PaymentWebhookInvalidError()));
        }
    }

    public async Task<Result<ProviderRefundResult>> RefundAsync(
        string providerReference,
        decimal? amount,
        CancellationToken cancellationToken = default)
    {
        var paymentIntentId = await ResolvePaymentIntentIdAsync(providerReference, cancellationToken);
        if (paymentIntentId is null)
        {
            return Result.Failure<ProviderRefundResult>(ErrorCodes.PaymentProviderFailedError());
        }

        var form = new Dictionary<string, string>
        {
            ["payment_intent"] = paymentIntentId,
        };

        if (amount.HasValue)
        {
            form["amount"] = ToMinorUnits(amount.Value).ToString(CultureInfo.InvariantCulture);
        }

        using var content = new FormUrlEncodedContent(form);
        using var response = await SendAsync(HttpMethod.Post, "/v1/refunds", content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Stripe refund failed with status {StatusCode}", response.StatusCode);
            return Result.Failure<ProviderRefundResult>(ErrorCodes.PaymentProviderFailedError());
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var status = root.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString() ?? "pending"
                : "pending";
            var refundedAmount = root.TryGetProperty("amount", out var amountProp) && amountProp.TryGetInt64(out var minor)
                ? minor / 100m
                : amount ?? 0m;

            return Result.Success(new ProviderRefundResult(providerReference, refundedAmount, status));
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            logger.LogError(ex, "Failed to parse Stripe refund response.");
            return Result.Failure<ProviderRefundResult>(ErrorCodes.PaymentProviderFailedError());
        }
    }

    internal static bool TryValidateWebhookSignature(
        string rawBody,
        IReadOnlyDictionary<string, string> headers,
        StripeOptions options)
    {
        if (!headers.TryGetValue("Stripe-Signature", out var headerValue)
            && !headers.TryGetValue("stripe-signature", out headerValue))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(headerValue) || string.IsNullOrWhiteSpace(options.WebhookSecret))
        {
            return false;
        }

        string? timestamp = null;
        var signatures = new List<string>();
        foreach (var part in headerValue.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            if (pieces.Length != 2)
            {
                continue;
            }

            if (pieces[0] == "t")
            {
                timestamp = pieces[1];
            }
            else if (pieces[0] == "v1")
            {
                signatures.Add(pieces[1]);
            }
        }

        if (timestamp is null || signatures.Count == 0)
        {
            return false;
        }

        if (options.WebhookToleranceSeconds > 0
            && long.TryParse(timestamp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            var eventTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            var skew = DateTimeOffset.UtcNow - eventTime;
            if (skew.Duration() > TimeSpan.FromSeconds(options.WebhookToleranceSeconds))
            {
                return false;
            }
        }

        var signedPayload = $"{timestamp}.{rawBody}";
        var computed = ComputeHmacSha256Hex(signedPayload, options.WebhookSecret);
        foreach (var signature in signatures)
        {
            if (FixedTimeEqualsHex(computed, signature))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<string?> ResolvePaymentIntentIdAsync(
        string providerReference,
        CancellationToken cancellationToken)
    {
        if (providerReference.StartsWith("pi_", StringComparison.Ordinal))
        {
            return providerReference;
        }

        using var response = await SendAsync(
            HttpMethod.Get,
            $"/v1/checkout/sessions/{Uri.EscapeDataString(providerReference)}",
            content: null,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.TryGetProperty("payment_intent", out var piProp))
            {
                return piProp.ValueKind switch
                {
                    JsonValueKind.String => piProp.GetString(),
                    JsonValueKind.Object when piProp.TryGetProperty("id", out var idProp) => idProp.GetString(),
                    _ => null,
                };
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Could not resolve Stripe payment_intent from Checkout Session.");
        }

        return null;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var options = stripeOptions.Value;
        using var request = new HttpRequestMessage(
            method,
            new Uri(new Uri(options.BaseUrl.TrimEnd('/') + "/"), path.TrimStart('/')));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.SecretKey);
        if (content is not null)
        {
            request.Content = content;
        }

        return await httpClient.SendAsync(request, cancellationToken);
    }

    private static string ResolveWebhookReference(JsonElement dataObject, string eventType)
    {
        if (dataObject.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        if (eventType.StartsWith("checkout.session", StringComparison.OrdinalIgnoreCase)
            && dataObject.TryGetProperty("id", out var sessionId)
            && sessionId.ValueKind == JsonValueKind.String)
        {
            return sessionId.GetString() ?? string.Empty;
        }

        if (dataObject.TryGetProperty("id", out var idProp)
            && idProp.ValueKind == JsonValueKind.String
            && (idProp.GetString()?.StartsWith("cs_", StringComparison.Ordinal) ?? false))
        {
            return idProp.GetString() ?? string.Empty;
        }

        if (dataObject.TryGetProperty("metadata", out var metadata)
            && metadata.ValueKind == JsonValueKind.Object
            && metadata.TryGetProperty("adeni_reference", out var adeniRef)
            && adeniRef.ValueKind == JsonValueKind.String)
        {
            return adeniRef.GetString() ?? string.Empty;
        }

        if (dataObject.TryGetProperty("client_reference_id", out var clientRef)
            && clientRef.ValueKind == JsonValueKind.String)
        {
            return clientRef.GetString() ?? string.Empty;
        }

        if (dataObject.TryGetProperty("payment_intent", out var paymentIntent))
        {
            return paymentIntent.ValueKind switch
            {
                JsonValueKind.String => paymentIntent.GetString() ?? string.Empty,
                JsonValueKind.Object when paymentIntent.TryGetProperty("id", out var piId) =>
                    piId.GetString() ?? string.Empty,
                _ => string.Empty,
            };
        }

        return dataObject.TryGetProperty("id", out var fallbackId) && fallbackId.ValueKind == JsonValueKind.String
            ? fallbackId.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string AppendSessionPlaceholder(string callbackUrl)
    {
        if (callbackUrl.Contains("{CHECKOUT_SESSION_ID}", StringComparison.Ordinal))
        {
            return callbackUrl;
        }

        var separator = callbackUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{callbackUrl}{separator}session_id={{CHECKOUT_SESSION_ID}}";
    }

    private static long ToMinorUnits(decimal amount) =>
        (long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    private static string ComputeHmacSha256Hex(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEqualsHex(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
