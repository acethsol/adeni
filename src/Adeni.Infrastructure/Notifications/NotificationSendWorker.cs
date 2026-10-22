namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Adeni.Application.Security;
using Microsoft.Extensions.Logging;

public sealed class NotificationSendWorker(
    INotificationDispatcher notificationDispatcher,
    ILogger<NotificationSendWorker> logger)
{
    public async Task SendAsync(NotificationJobPayload payload, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Notification job send channel={Channel} dedupe={DedupeEventId} recipient={Recipient}",
            payload.Channel,
            payload.DedupeEventId,
            MaskRecipient(payload.Channel, payload.RecipientKey));

        await notificationDispatcher.SendAsync(payload.ToMessage(), cancellationToken);
    }

    private static string MaskRecipient(string channel, string recipientKey) =>
        channel.Trim().ToLowerInvariant() switch
        {
            "email" => PiiMasker.MaskEmail(recipientKey),
            "sms" or "whatsapp" => PiiMasker.MaskPhone(recipientKey),
            _ => PiiMasker.MaskEmail(recipientKey),
        };
}
