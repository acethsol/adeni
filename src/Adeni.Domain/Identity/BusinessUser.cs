namespace Adeni.Domain.Identity;

using Adeni.Domain.Tenancy;

public sealed class BusinessUser : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Auth0Sub { get; set; } = string.Empty;

    public string Role { get; set; } = "owner";

    public DateTimeOffset CreatedAt { get; set; }

    public Tenant? Tenant { get; set; }
}
