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

    public PricingType PricingType { get; set; } = PricingType.Fixed;

    public int DurationMinutes { get; set; }

    public string? CategorySlug { get; set; }

    public string? CatalogServiceId { get; set; }

    public BookingDeliveryType BookingDeliveryType { get; set; } = BookingDeliveryType.Appointment;

    /// <summary>Optional public-menu collection; null = ungrouped.</summary>
    public Guid? MenuGroupId { get; set; }

    /// <summary>Order within the menu group (or among ungrouped services).</summary>
    public int SortOrder { get; set; }

    /// <summary>Add-on service — cannot be booked alone without a primary service.</summary>
    public bool IsAddOn { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ServiceMenuGroup? MenuGroup { get; set; }
}
