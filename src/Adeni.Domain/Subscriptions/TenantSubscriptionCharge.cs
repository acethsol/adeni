namespace Adeni.Domain.Subscriptions;

/// <summary>Synced Adeni SaaS charge/invoice snapshot (business → Adeni). Not customer receipts.</summary>
public sealed class TenantSubscriptionCharge
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Provider event idempotency key, e.g. subscription.create:SUB_abc.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public string ProviderReference { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public SubscriptionSku? SubscriptionSku { get; set; }

    public long? AmountMinor { get; set; }

    public string? Currency { get; set; }

    public string Status { get; set; } = "pending";

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
