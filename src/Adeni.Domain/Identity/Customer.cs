namespace Adeni.Domain.Identity;

public sealed class Customer
{
    public Guid Id { get; set; }

    public string Auth0Sub { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    /// <summary>Optional platform locale (en, fr) for notification rendering.</summary>
    public string? PreferredLocale { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ErasureRequestedAt { get; set; }

    public DateTimeOffset? EmailVerifiedAt { get; set; }

    public DateTimeOffset? PhoneVerifiedAt { get; set; }

    /// <summary>HMAC-SHA256 (hex) of normalized E.164 at last phone verification.</summary>
    public string? PhoneVerifiedFingerprint { get; set; }

    public DateTimeOffset? TrustUpdatedAt { get; set; }
}
