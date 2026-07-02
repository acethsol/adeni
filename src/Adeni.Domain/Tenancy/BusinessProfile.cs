namespace Adeni.Domain.Tenancy;

/// <summary>Brand-level profile (1:1 with tenant). Branches live in <see cref="BusinessLocation"/>.</summary>
public sealed class BusinessProfile : ITenantEntity
{
    public Guid TenantId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string CategorySlug { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant? Tenant { get; set; }

    public ICollection<BusinessLocation> Locations { get; set; } = new List<BusinessLocation>();
}
