namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

public sealed class ServiceOffering : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal PriceAmount { get; set; }

    public string Currency { get; set; } = "NGN";

    public int DurationMinutes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
