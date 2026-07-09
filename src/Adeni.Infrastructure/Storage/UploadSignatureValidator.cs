namespace Adeni.Infrastructure.Storage;

using System.Security.Cryptography;
using System.Text;
using Adeni.Application.Storage;
using Microsoft.Extensions.Options;

public sealed class UploadSignatureValidator(IOptions<StorageOptions> options)
{
    public bool TryValidate(string storageKey, long expiresUnixSeconds, string signature)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnixSeconds)
        {
            return false;
        }

        var expected = Sign(storageKey, expiresUnixSeconds);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    public string Sign(string storageKey, long expiresUnixSeconds)
    {
        var payload = $"{storageKey}|{expiresUnixSeconds}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.Value.Local.UploadSigningKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
