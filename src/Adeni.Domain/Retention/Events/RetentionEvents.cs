namespace Adeni.Domain.Retention.Events;

using Adeni.Domain.Events;

public sealed record CustomerPackageActivated(
    Guid CustomerPackageId,
    Guid TenantId,
    Guid TenantCustomerId,
    DateTimeOffset At) : IDomainEvent;

public sealed record BenefitReserved(
    Guid BenefitBalanceId,
    Guid TenantId,
    Guid BookingId,
    int Quantity,
    DateTimeOffset At) : IDomainEvent;

public sealed record BenefitRedeemed(
    Guid BenefitBalanceId,
    Guid TenantId,
    Guid BookingId,
    int Quantity,
    DateTimeOffset At) : IDomainEvent;

public sealed record BenefitReleased(
    Guid BenefitBalanceId,
    Guid TenantId,
    Guid? BookingId,
    int Quantity,
    DateTimeOffset At) : IDomainEvent;

public sealed record CustomerPackageExpired(
    Guid CustomerPackageId,
    Guid TenantId,
    DateTimeOffset At) : IDomainEvent;

public sealed record CustomerPackageVoided(
    Guid CustomerPackageId,
    Guid TenantId,
    DateTimeOffset At) : IDomainEvent;
public sealed record CustomerMembershipActivated(
    Guid CustomerMembershipId,
    Guid TenantId,
    Guid TenantCustomerId,
    DateTimeOffset At) : IDomainEvent;

public sealed record CustomerMembershipCancelled(
    Guid CustomerMembershipId,
    Guid TenantId,
    bool Immediate,
    DateTimeOffset At) : IDomainEvent;
