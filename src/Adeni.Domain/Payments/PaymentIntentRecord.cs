namespace Adeni.Domain.Payments;

using Adeni.Domain.Tenancy;

public sealed class PaymentIntentRecord : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid? BookingId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "NGN";

    public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.Pending;

    public string ProviderReference { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
