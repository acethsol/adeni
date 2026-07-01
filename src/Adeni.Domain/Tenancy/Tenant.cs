namespace Adeni.Domain.Tenancy;

public sealed class Tenant
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public TenantStatus Status { get; set; } = TenantStatus.Draft;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }
}
