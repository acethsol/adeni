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

/// <summary>A paid renewal started <paramref name="CycleNumber"/> (Sprint 37.2-C). Notifications consume in 38B+.</summary>
public sealed record CustomerMembershipRenewed(
    Guid CustomerMembershipId,
    Guid TenantId,
    Guid TenantCustomerId,
    int CycleNumber,
    DateTimeOffset At) : IDomainEvent;

/// <summary>A renewal charge failed or went unpaid; the membership is <c>PastDue</c> (Sprint 37.2-C). Reason is a code, never PII.</summary>
public sealed record CustomerMembershipPaymentFailed(
    Guid CustomerMembershipId,
    Guid TenantId,
    Guid TenantCustomerId,
    string? Reason,
    DateTimeOffset At) : IDomainEvent;

/// <summary>Grace ran out without payment; remaining credits were forfeited (Sprint 37.2-C).</summary>
public sealed record CustomerMembershipExpired(
    Guid CustomerMembershipId,
    Guid TenantId,
    Guid TenantCustomerId,
    DateTimeOffset At) : IDomainEvent;

/// <summary>Points credited for a paid completed visit (Sprint 37.3-B). Notifications consume in 38B+.</summary>
public sealed record LoyaltyPointsEarned(
    Guid TenantId,
    Guid TenantCustomerId,
    Guid BookingId,
    int Points,
    int BalanceAfter,
    DateTimeOffset At) : IDomainEvent;
