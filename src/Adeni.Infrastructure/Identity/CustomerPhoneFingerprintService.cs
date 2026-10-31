namespace Adeni.Infrastructure.Identity;

using System.Security.Cryptography;
using System.Text;
using Adeni.Application.Identity;
using Microsoft.Extensions.Options;

public sealed class CustomerPhoneFingerprintService(IOptions<CustomerVerificationOptions> options)
    : ICustomerPhoneFingerprintService
{
    public string? ComputeFingerprint(string? normalizedE164Phone)
    {
        if (string.IsNullOrWhiteSpace(normalizedE164Phone))
        {
            return null;
        }

        var key = ResolveKey();
        if (key.Length == 0)
        {
            return null;
        }

        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(normalizedE164Phone.Trim()));
        return Convert.ToHexString(hash);
    }

    public bool PhoneMatchesFingerprint(string? normalizedE164Phone, string? storedFingerprint)
    {
        if (string.IsNullOrWhiteSpace(storedFingerprint))
        {
            return false;
        }

        var computed = ComputeFingerprint(normalizedE164Phone);
        return computed is not null
            && string.Equals(computed, storedFingerprint, StringComparison.OrdinalIgnoreCase);
    }

    private byte[] ResolveKey()
    {
        var configured = options.Value.PhoneVerificationHmacKey?.Trim() ?? string.Empty;
        return configured.Length == 0 ? [] : Encoding.UTF8.GetBytes(configured);
    }
}
