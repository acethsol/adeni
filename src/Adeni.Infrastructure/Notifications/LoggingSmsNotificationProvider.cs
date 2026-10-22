namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Adeni.Application.Security;
using Microsoft.Extensions.Logging;

public sealed class LoggingSmsNotificationProvider(ILogger<LoggingSmsNotificationProvider> logger)
    : ISmsNotificationProvider
{
    public Task SendAsync(SmsNotificationRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Notification [sms] (log-only) from {From} to {Recipient}",
            request.FromPhoneOrSenderId,
            PiiMasker.MaskPhone(request.ToPhoneE164));
        return Task.CompletedTask;
    }
}
