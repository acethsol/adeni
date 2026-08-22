namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

public sealed class QuoteRequestRecord : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CustomerId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? ServiceAddress { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
