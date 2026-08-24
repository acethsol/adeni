namespace Adeni.Domain.Payments.Events;

using Adeni.Domain.Events;

public sealed record PaymentCompleted(
    Guid PaymentIntentId,
    Guid TenantId,
    Guid? BookingId,
    PaymentIntentType Type,
    decimal Amount,
    string Currency,
    string ProviderReference,
    DateTimeOffset At) : IDomainEvent;
