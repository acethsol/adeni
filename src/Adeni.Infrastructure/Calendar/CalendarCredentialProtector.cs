namespace Adeni.Infrastructure.Calendar;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

public sealed class CalendarCredentialProtector(IOptions<CalendarOptions> options)
{
    public string Protect(Guid tenant, Guid id, string provider, string value)
    {
        var o = options.Value;
        var key = Convert.FromBase64String(o.EncryptionKeys[o.CurrentKeyVersion]);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var input = Encoding.UTF8.GetBytes(value); var output = new byte[input.Length]; var tag = new byte[16];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, input, output, tag, Encoding.UTF8.GetBytes($"Calendar.v1:{tenant:N}:{id:N}:{provider}"));
            return string.Join('.', o.CurrentKeyVersion, Convert.ToBase64String(nonce), Convert.ToBase64String(tag), Convert.ToBase64String(output));
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(input); }
    }
    public string Unprotect(Guid tenant, Guid id, string provider, string envelope)
    {
        try
        {
            var parts = envelope.Split('.');
            if (parts.Length != 4) throw new CryptographicException();
            var key = Convert.FromBase64String(options.Value.EncryptionKeys[parts[0]]);
            var encrypted = Convert.FromBase64String(parts[3]); var plain = new byte[encrypted.Length];
            try
            {
                using var aes = new AesGcm(key, 16);
                aes.Decrypt(Convert.FromBase64String(parts[1]), encrypted, Convert.FromBase64String(parts[2]), plain,
                    Encoding.UTF8.GetBytes($"Calendar.v1:{tenant:N}:{id:N}:{provider}"));
                return Encoding.UTF8.GetString(plain);
            }
            finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or KeyNotFoundException or ArgumentException)
        { throw new CalendarProviderException("credential_unavailable"); }
    }
    internal static string Secret() => Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
