namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Events;
using Adeni.Application.Notifications;
using Adeni.Domain.Messaging;
using Adeni.Domain.Messaging.Events;
using Microsoft.Extensions.Logging;

/// <summary>
/// Prefs-aware “new message” notifications.
/// </summary>
public sealed class MessagingNotificationHandler(
    INotificationDispatcher notificationDispatcher,
    INotificationRecipientResolver recipientResolver,
    ILogger<MessagingNotificationHandler> logger)
    : IDomainEventHandler<ConversationMessageCreated>
{
    public async Task HandleAsync(
        ConversationMessageCreated domainEvent,
        CancellationToken cancellationToken = default)
    {
        var preview = BuildPreview(domainEvent.BodyPreview, domainEvent.HasAttachments);
        var data = new Dictionary<string, string>
        {
            ["preview"] = preview,
            ["conversationId"] = domainEvent.ConversationId.ToString("D"),
        };

        if (domainEvent.SenderType == ConversationParticipantType.Customer)
        {
            await notificationDispatcher.SendAsync(
                NotificationMessage.FromTemplate(
                    "email",
                    string.Empty,
                    NotificationTemplateIds.ConversationBusiness,
                    data,
                    domainEvent.TenantId,
                    $"conversation-business:{domainEvent.MessageId:D}"),
                cancellationToken);
            return;
        }

        var contact = await recipientResolver.ResolvePlatformCustomerAsync(
            domainEvent.CustomerId,
            cancellationToken);

        if (contact.IsFailure || string.IsNullOrWhiteSpace(contact.Value!.Email))
        {
            logger.LogDebug(
                "Skipping customer message email for conversation {ConversationId} — no email on file.",
                domainEvent.ConversationId);
            return;
        }

        await notificationDispatcher.SendAsync(
            NotificationMessage.FromTemplate(
                "email",
                contact.Value.Email.Trim(),
                NotificationTemplateIds.ConversationCustomer,
                data,
                domainEvent.TenantId,
                $"conversation-customer:{domainEvent.MessageId:D}",
                domainEvent.CustomerId),
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
