namespace Adeni.Domain.Identity;

using Adeni.Domain.Tenancy;

public sealed class BusinessUserDashboardLayout : ITenantEntity
{
    public Guid BusinessUserId { get; set; }

    public Guid TenantId { get; set; }

    public string LayoutJson { get; set; } = "{}";

    public DateTimeOffset UpdatedAt { get; set; }

    public BusinessUser? BusinessUser { get; set; }
}
