namespace Adeni.Infrastructure.Finance;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public sealed class CommerceOptions
{
    public const string SectionName = "Finance:Commerce";
    public string PublicBaseUrl { get; set; } = "http://localhost:5190";
    public string ActiveKeyVersion { get; set; } = "v1";
    public Dictionary<string, string> CredentialKeys { get; set; } = [];
}

// Separate purposes prevent a share token from being used as a gift code.
public sealed class CommerceCredentials(IOptions<CommerceOptions> options, IHostEnvironment environment)
{
    public string KeyVersion => options.Value.ActiveKeyVersion;
    public static string NewNonce() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static bool Valid(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public string Derive(string purpose, Guid tenant, Guid id, string nonce, string version)
    {
        byte[] key;
        if (options.Value.CredentialKeys.TryGetValue(version, out var encoded)) key = Convert.FromBase64String(encoded);
        else if (environment.IsDevelopment() || environment.EnvironmentName == "Testing")
            key = SHA256.HashData(Encoding.UTF8.GetBytes("adeni-commerce-development-only-not-for-live-sales"));
        else throw new InvalidOperationException("Commerce credential key is unavailable.");
        if (key.Length < 32) throw new InvalidOperationException("Commerce credential key must contain at least 256 bits.");
        return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{purpose}:{tenant:N}:{id:N}:{nonce}")));
    }
    public string GiftCode(Adeni.Domain.Finance.BusinessGiftCard card) => Derive("gift", card.TenantId, card.Id, card.Nonce, card.KeyVersion);
    public string LinkToken(Adeni.Domain.Finance.CommercePaymentRequest request) => Derive("link", request.TenantId, request.Id, request.Nonce, request.KeyVersion);
    public string ShareUrl(Adeni.Domain.Finance.CommercePaymentRequest request) => $"{options.Value.PublicBaseUrl.TrimEnd('/')}/pay#t={LinkToken(request)}";
}
