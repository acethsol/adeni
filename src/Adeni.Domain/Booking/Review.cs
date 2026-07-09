namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

public sealed class Review : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid BookingId { get; set; }

    public Guid CustomerId { get; set; }

    public byte Rating { get; set; }

    public string Comment { get; set; } = string.Empty;

    public bool IsHidden { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? HiddenAt { get; set; }
}
