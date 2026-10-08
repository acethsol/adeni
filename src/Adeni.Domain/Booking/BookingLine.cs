namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

/// <summary>One service in a multi-service booking cart (sequential same visit).</summary>
public sealed class BookingLine : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public Guid TenantId { get; set; }

    public Guid ServiceOfferingId { get; set; }

    public int SortOrder { get; set; }

    public decimal PriceAmount { get; set; }

    public int DurationMinutes { get; set; }

    public string ServiceName { get; set; } = string.Empty;

    public bool IsAddOn { get; set; }

    public BookingRecord? Booking { get; set; }

    public ServiceOffering? ServiceOffering { get; set; }
}
