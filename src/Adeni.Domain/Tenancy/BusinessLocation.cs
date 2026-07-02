namespace Adeni.Domain.Tenancy;

public sealed class BusinessLocation : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>URL slug — unique across active locations (public /businesses/{slug}).</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Branch display name (e.g. "Lekki", "Victoria Island").</summary>
    public string Name { get; set; } = string.Empty;

    public string MarketId { get; set; } = string.Empty;

    public string AddressLine { get; set; } = string.Empty;

    public string Area { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>Optional IANA time zone for this branch's availability.</summary>
    public string? TimeZoneId { get; set; }

    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant? Tenant { get; set; }
}
