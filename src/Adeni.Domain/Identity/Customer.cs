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
}
