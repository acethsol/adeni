namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Adeni.Application.Security;
using Microsoft.Extensions.Logging;

public sealed class LoggingNotificationDispatcher(ILogger<LoggingNotificationDispatcher> logger)
    : INotificationDispatcher
{
    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var maskedRecipient = message.Channel.Trim().ToLowerInvariant() switch
        {
            "email" => PiiMasker.MaskEmail(message.RecipientKey),
            "sms" or "whatsapp" => PiiMasker.MaskPhone(message.RecipientKey),
            _ => message.RecipientKey,
        };

        logger.LogInformation(
            "Notification [{Channel}] to {Recipient}: {Subject}",
            message.Channel,
            maskedRecipient,
            message.Subject);
        return Task.CompletedTask;
    }
}
