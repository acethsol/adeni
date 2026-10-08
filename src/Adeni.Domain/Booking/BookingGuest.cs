namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

/// <summary>Optional named guest on a multi-guest booking (no Auth0 account).</summary>
public sealed class BookingGuest : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public Guid TenantId { get; set; }

    public string? DisplayName { get; set; }

    public int SortOrder { get; set; }

    public BookingRecord? Booking { get; set; }
}
