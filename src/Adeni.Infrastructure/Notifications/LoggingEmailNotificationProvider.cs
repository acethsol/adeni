namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Adeni.Application.Security;
using Microsoft.Extensions.Logging;

public sealed class LoggingEmailNotificationProvider(ILogger<LoggingEmailNotificationProvider> logger)
    : IEmailNotificationProvider
{
    public Task SendAsync(EmailNotificationRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Notification [email] (log-only) from {From} to {Recipient}: {Subject}",
            request.FromEmail,
            PiiMasker.MaskEmail(request.ToEmail),
            request.Subject);
        return Task.CompletedTask;
    }
}
