namespace Adeni.Domain.Booking.Events;

using Adeni.Domain.Events;

public sealed record BookingRejected(
    Guid BookingId,
    Guid TenantId,
    Guid CustomerId,
    string? Reason,
    DateTimeOffset At) : IDomainEvent;
