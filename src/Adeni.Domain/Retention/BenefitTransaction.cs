namespace Adeni.Domain.Retention;

using Adeni.Domain.Tenancy;

/// <summary>Append-only ledger row. Never updated or deleted.</summary>
public sealed class BenefitTransaction : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid BenefitBalanceId { get; set; }

    public int Sequence { get; set; }

    public BenefitTransactionKind Kind { get; set; }

    /// <summary>Unsigned units moved.</summary>
    public int Quantity { get; set; }

    /// <summary>Signed change to Remaining (0 for Redeem: reserved → used).</summary>
    public int Delta { get; set; }

    public int ReservedAfter { get; set; }

    public int UsedAfter { get; set; }

    public int RemainingAfter { get; set; }

    public Guid? BookingId { get; set; }

    public Guid? BusinessLocationId { get; set; }

    public string Actor { get; set; } = string.Empty;

    public string? Reason { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}

/// <summary>Idempotency receipt for a retention mutation (sell / void). Metadata only.</summary>
public sealed class BenefitOperation : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string KeyHash { get; set; } = string.Empty;

    public string Fingerprint { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public string Actor { get; set; } = string.Empty;

    public Guid? ResultCustomerPackageId { get; set; }

    /// <summary>Set for membership enrolments (37.2-B); mutually exclusive with <see cref="ResultCustomerPackageId"/>.</summary>
    public Guid? ResultCustomerMembershipId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}