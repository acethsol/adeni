namespace Adeni.Domain.Tenancy;

public sealed class BusinessProfileCategory : ITenantEntity
{
    public Guid TenantId { get; set; }

    public string CategorySlug { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public BusinessProfile? Profile { get; set; }
}
