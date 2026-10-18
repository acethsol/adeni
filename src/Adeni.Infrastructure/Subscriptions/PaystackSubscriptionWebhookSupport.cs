namespace Adeni.Infrastructure.Subscriptions;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class PaystackSubscriptionWebhookSupport
{
    public static bool TryValidateSignature(
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

    public static bool TryGetMetadataString(JsonElement metadata, string key, out string? value)
    {
        value = null;
        if (metadata.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!metadata.TryGetProperty(key, out var prop))
        {
            return false;
        }

        value = prop.GetString();
        return !string.IsNullOrWhiteSpace(value);
    }

    public static Guid? TryGetMetadataTenantId(JsonElement metadata)
    {
        if (!TryGetMetadataString(metadata, "tenant_id", out var raw)
            && !TryGetMetadataString(metadata, "tenantId", out raw))
        {
            return null;
        }

        return Guid.TryParse(raw, out var tenantId) ? tenantId : null;
    }

    public static string? TryGetMetadataSku(JsonElement metadata)
    {
        if (TryGetMetadataString(metadata, "target_sku", out var sku))
        {
            return sku;
        }

        if (TryGetMetadataString(metadata, "targetSku", out sku))
        {
            return sku;
        }

        if (TryGetMetadataString(metadata, "target_tier", out sku))
        {
            return sku;
        }

        return TryGetMetadataString(metadata, "targetTier", out sku) ? sku : null;
    }

    public static JsonElement GetDataObject(JsonElement root) =>
        root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
            ? data
            : default;

    public static JsonElement GetMetadata(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("metadata", out var metadata))
        {
            return metadata;
        }

        return default;
    }
}
