namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

public sealed class BookingRecord : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ServiceOfferingId { get; set; }

    public Guid CustomerId { get; set; }

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public string? CustomerNotes { get; set; }

    public string? BusinessNotes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ServiceOffering? ServiceOffering { get; set; }
}
