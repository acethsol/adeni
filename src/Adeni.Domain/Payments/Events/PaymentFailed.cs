namespace Adeni.Domain.Payments.Events;

using Adeni.Domain.Events;

public sealed record PaymentFailed(
    Guid PaymentIntentId,
    Guid TenantId,
    Guid? BookingId,
    PaymentIntentType Type,
    string? Reason,
    DateTimeOffset At) : IDomainEvent;
