namespace Adeni.Infrastructure.Finance;

using System.Security.Cryptography;
using System.Text;
using Adeni.Application.Finance;
using Microsoft.Extensions.Options;

public sealed class PaymentCheckoutRealtimeTokenService(IOptions<FinanceOptions> options)
    : IPaymentCheckoutRealtimeTokenService
{
    public string Issue(Guid paymentIntentId, string providerReference, TimeSpan? ttl = null)
    {
        var reference = providerReference.Trim();
        if (paymentIntentId == Guid.Empty || string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException("Payment intent and provider reference are required.");
        }

        var lifetime = ttl ?? TimeSpan.FromMinutes(Math.Max(1, options.Value.CheckoutRealtimeTokenMinutes));
        var expiresUnix = DateTimeOffset.UtcNow.Add(lifetime).ToUnixTimeSeconds();
        var payload = $"{paymentIntentId:D}|{reference}|{expiresUnix}";
        var signature = Sign(payload);
        return $"{Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))}.{signature}";
    }

    public bool TryResolve(string token, out PaymentCheckoutRealtimeClaims? claims)
    {
        claims = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Trim().Split('.', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] payloadBytes;
        try
        {
            payloadBytes = Convert.FromBase64String(parts[0]);
        }
        catch (FormatException)
        {
            return false;
        }

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var expected = Sign(payload);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(parts[1])))
        {
            return false;
        }

        var segments = payload.Split('|', 3);
        if (segments.Length != 3
            || !Guid.TryParse(segments[0], out var paymentIntentId)
            || !long.TryParse(segments[2], out var expiresUnix))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnix)
        {
            return false;
        }

        var reference = segments[1].Trim();
        if (string.IsNullOrWhiteSpace(reference))
        {
            return false;
        }

        claims = new PaymentCheckoutRealtimeClaims(paymentIntentId, reference);
        return true;
    }

    private string Sign(string payload)
    {
        var key = options.Value.CheckoutRealtimeSigningKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Finance:CheckoutRealtimeSigningKey is not configured.");
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
