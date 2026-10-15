namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

/// <summary>Inclusive calendar date range when the business accepts no bookings.</summary>
public sealed class BusinessClosedDate : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    /// <summary>Owner-only note; not shown on public APIs.</summary>
    public string? Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
