namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

public sealed class BookingRecord : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ServiceOfferingId { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>Null = customer chose “any available”; business may assign later.</summary>
    public Guid? StaffMemberId { get; set; }

    /// <summary>Party size; MVP appointment duration scales by this count.</summary>
    public int GuestCount { get; set; } = 1;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public string? CustomerNotes { get; set; }

    public string? BusinessNotes { get; set; }

    public string? IdempotencyKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ServiceOffering? ServiceOffering { get; set; }

    public StaffMember? StaffMember { get; set; }

    public ICollection<BookingLine> Lines { get; set; } = new List<BookingLine>();

    public ICollection<BookingGuest> Guests { get; set; } = new List<BookingGuest>();
}
