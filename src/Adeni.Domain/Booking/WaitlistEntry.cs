namespace Adeni.Domain.Booking;

using Adeni.Domain.Tenancy;

public sealed class WaitlistEntry : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ServiceOfferingId { get; set; }

    public Guid CustomerId { get; set; }

    public DateTimeOffset? PreferredFrom { get; set; }

    public DateTimeOffset? PreferredTo { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? NotifiedAt { get; set; }
}
