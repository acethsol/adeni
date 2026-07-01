namespace Adeni.Domain.Tenancy;

public sealed class BusinessProfile : ITenantEntity
{
    public Guid TenantId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string CategorySlug { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string AddressLine { get; set; } = string.Empty;

    public string Area { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant? Tenant { get; set; }
}
