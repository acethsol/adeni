namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Microsoft.Extensions.Logging;

public sealed class LoggingNotificationDispatcher(ILogger<LoggingNotificationDispatcher> logger)
    : INotificationDispatcher
{
    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Notification [{Channel}] to {Recipient}: {Subject} — {Body}",
            message.Channel,
            message.RecipientKey,
            message.Subject,
            message.Body);
        return Task.CompletedTask;
    }
}
