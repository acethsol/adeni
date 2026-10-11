namespace Adeni.Domain.Payments;

using Adeni.Domain.Tenancy;

public sealed class BillingDocument : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public BillingDocumentKind Kind { get; set; }

    public BillingDocumentStatus Status { get; set; } = BillingDocumentStatus.Draft;

    public string DocumentNumber { get; set; } = string.Empty;

    public Guid? BookingId { get; set; }

    public Guid? PaymentIntentId { get; set; }

    public string BusinessNameSnapshot { get; set; } = string.Empty;

    public string? CustomerNameSnapshot { get; set; }

    public string? CustomerEmailSnapshot { get; set; }

    public string Currency { get; set; } = "NGN";

    public decimal Subtotal { get; set; }

    public decimal? TaxTotal { get; set; }

    public decimal Total { get; set; }

    public string? MethodLabel { get; set; }

    public string PublicToken { get; set; } = string.Empty;

    public string? VoidReason { get; set; }

    public DateTimeOffset? IssuedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<BillingDocumentLine> Lines { get; set; } = new List<BillingDocumentLine>();
}

public sealed class BillingDocumentLine : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public Guid TenantId { get; set; }

    public int SortOrder { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1m;

    public decimal UnitAmount { get; set; }

    public decimal LineTotal { get; set; }

    public BillingDocument? Document { get; set; }
}

public sealed class BillingDocumentSequence : ITenantEntity
{
    public Guid TenantId { get; set; }

    public BillingDocumentKind Kind { get; set; }

    public int NextValue { get; set; } = 1;
}
