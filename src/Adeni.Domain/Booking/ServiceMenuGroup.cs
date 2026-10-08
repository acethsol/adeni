namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

/// <summary>Named collection under which services appear on the public menu (Zenoti-style).</summary>
public sealed class ServiceMenuGroup : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
