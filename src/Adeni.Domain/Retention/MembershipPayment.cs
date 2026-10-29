namespace Adeni.Domain.Retention;

using Adeni.Domain.Tenancy;

/// <summary>
/// One provider charge for one billing cycle of a <see cref="CustomerMembership"/> (Sprint 37.2-C). It links the
/// Finance payment intent to the cycle it pays for, so the <c>PaymentCompleted</c> handler is idempotent
/// (Murphy): a cycle is applied at most once, enforced by status and by unique indexes on the intent and on
/// (membership, cycle) for open/succeeded rows. Metadata only — Finance owns the money, Retention owns no balance.
/// </summary>
public sealed class MembershipPayment : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CustomerMembershipId { get; set; }

    /// <summary>The cycle this charge pays for (1 = first / initial).</summary>
    public int CycleNumber { get; set; }

    public MembershipPaymentKind Kind { get; set; }

    public Guid PaymentIntentId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "NGN";

    public MembershipPaymentStatus Status { get; set; }

    /// <summary>Provider's reference for the confirmed charge (set on success).</summary>
    public string? ProviderReference { get; set; }

    /// <summary>Provider failure code/reason (set on failure). Never raw payloads.</summary>
    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
