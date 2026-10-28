namespace Adeni.Domain.Finance;

using Adeni.Domain.Tenancy;

public enum PaymentRequestPurpose { CustomCharge, GiftCardPurchase }
public enum PaymentRequestState { Active, PaymentPending, Paid, Cancelled, Refunded, RefundPending }
public enum GiftCardState { PendingPayment, Active, Exhausted, Suspended, Voided }
public enum GiftTransactionKind { Issue, Redeem, RedemptionReversal, Void }

public sealed class CommercePaymentRequest : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public PaymentRequestPurpose Purpose { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public string Label { get; set; } = "";
    public bool AllowsGiftCard { get; set; }
    public PaymentRequestState State { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string Nonce { get; set; } = "";
    public string KeyVersion { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public byte[] RowVersion { get; set; } = [];
    public override string ToString() => $"PaymentRequest {Id} {State}";
}

public sealed class CommercePaymentAttempt : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RequestId { get; set; }
    public Guid PaymentIntentId { get; set; }
    public string Provider { get; set; } = "";
    public string MerchantReference { get; set; } = "";
    public string? CheckoutUrl { get; set; }
    public string State { get; set; } = "initializing";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public override string ToString() => $"PaymentAttempt {Id} {State}";
}

public sealed class BusinessGiftCard : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid PurchaseRequestId { get; set; }
    public Guid? PurchaserCustomerId { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Currency { get; set; } = "NGN";
    public GiftCardState State { get; set; }
    public string Nonce { get; set; } = "";
    public string KeyVersion { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public string CodeSuffix { get; set; } = "";
    public int Sequence { get; set; }
    public DateTimeOffset? IssuedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public override string ToString() => $"BusinessGiftCard {Id} {State}";
}

public sealed class GiftCardTransaction : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid GiftCardId { get; set; }
    public Guid RequestId { get; set; }
    public Guid? ReversesTransactionId { get; set; }
    public int Sequence { get; set; }
    public GiftTransactionKind Kind { get; set; }
    public decimal Delta { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Actor { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class CommerceSettlement : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RequestId { get; set; }
    public Guid? PaymentIntentId { get; set; }
    public Guid? GiftTransactionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public DateTimeOffset SettledAt { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

// Durable, metadata-only operation receipt; committed with its financial mutation.
public sealed class CommerceOperation : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid RequestId { get; set; }
    public string KeyHash { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Actor { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}
