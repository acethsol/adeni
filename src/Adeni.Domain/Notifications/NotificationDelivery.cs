namespace Adeni.Domain.Notifications;

public enum NotificationDeliveryStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
}

public sealed class NotificationDelivery
{
    public Guid Id { get; set; }

    public string DedupeKey { get; set; } = string.Empty;

    public Guid? TenantId { get; set; }

    public string Channel { get; set; } = string.Empty;

    public string RecipientKey { get; set; } = string.Empty;

    public NotificationDeliveryStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}
