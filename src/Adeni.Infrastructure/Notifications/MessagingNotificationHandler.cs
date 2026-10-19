namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Events;
using Adeni.Application.Notifications;
using Adeni.Domain.Messaging;
using Adeni.Domain.Messaging.Events;

/// <summary>
/// Prefs-aware “new message” notifications. Delivery is still the logging stub until adapters land.
/// </summary>
public sealed class MessagingNotificationHandler(INotificationDispatcher notificationDispatcher)
    : IDomainEventHandler<ConversationMessageCreated>
{
    public Task HandleAsync(
        ConversationMessageCreated domainEvent,
        CancellationToken cancellationToken = default)
    {
        var preview = BuildPreview(domainEvent.BodyPreview, domainEvent.HasAttachments);

        // Customer → business inbox alert (tenant prefs gate email/push).
        if (domainEvent.SenderType == ConversationParticipantType.Customer)
        {
            return notificationDispatcher.SendAsync(
                new NotificationMessage(
                    "email",
                    $"tenant:{domainEvent.TenantId:D}",
                    "New customer message",
                    $"New message in conversation {domainEvent.ConversationId:D}: {preview}",
                    domainEvent.TenantId),
                cancellationToken);
        }

        // Business / bot → customer alert (same tenant prefs gate as booking emails).
        return notificationDispatcher.SendAsync(
            new NotificationMessage(
                "email",
                domainEvent.CustomerId.ToString("D"),
                "New message from business",
                $"You have a new message about your Adeni conversation: {preview}",
                domainEvent.TenantId),
            cancellationToken);
    }

    private static string BuildPreview(string? bodyPreview, bool hasAttachments)
    {
        if (!string.IsNullOrWhiteSpace(bodyPreview))
        {
            var trimmed = bodyPreview.Trim();
            return trimmed.Length <= 80 ? trimmed : trimmed[..80] + "…";
        }

        return hasAttachments ? "Attachment" : "New message";
    }
}
