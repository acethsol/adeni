namespace Adeni.Domain.Payments;

using Adeni.Domain.Tenancy;

public sealed class PaymentIntentRecord : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid? BookingId { get; set; }

    public PaymentIntentType Type { get; set; } = PaymentIntentType.Deposit;

    public decimal Amount { get; set; }

    public decimal PlatformFeeAmount { get; set; }

    public string Currency { get; set; } = "NGN";

    public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.Pending;

    public string ProviderReference { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? CustomerEmail { get; set; }

    public string? CallbackUrl { get; set; }

    public string? IdempotencyKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
