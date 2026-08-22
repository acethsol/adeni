namespace Adeni.Domain.Booking.Events;

using Adeni.Domain.Events;

public sealed record BookingCancelled(
    Guid BookingId,
    Guid TenantId,
    Guid CustomerId,
    DateTimeOffset StartAt,
    DateTimeOffset At) : IDomainEvent;
