namespace Adeni.Domain.Booking.Events;

using Adeni.Domain.Events;

public sealed record SlotOpened(
    Guid TenantId,
    Guid ServiceOfferingId,
    DateTimeOffset StartAt,
    DateTimeOffset At) : IDomainEvent;
