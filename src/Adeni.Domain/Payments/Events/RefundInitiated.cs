namespace Adeni.Domain.Payments.Events;

using Adeni.Domain.Events;

public sealed record RefundInitiated(
    Guid PaymentIntentId,
    Guid TenantId,
    Guid? BookingId,
    decimal Amount,
    string Currency,
    string ProviderReference,
    DateTimeOffset At) : IDomainEvent;
