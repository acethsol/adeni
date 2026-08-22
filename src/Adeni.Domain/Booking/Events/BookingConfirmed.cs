namespace Adeni.Domain.Booking.Events;

using Adeni.Domain.Events;

public sealed record BookingConfirmed(
    Guid BookingId,
    Guid TenantId,
    Guid CustomerId,
    DateTimeOffset StartAt,
    DateTimeOffset At) : IDomainEvent;
