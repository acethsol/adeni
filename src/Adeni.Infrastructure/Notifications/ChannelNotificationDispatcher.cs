namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Microsoft.Extensions.Logging;

public sealed class ChannelNotificationDispatcher(
    IEmailNotificationProvider emailProvider,
    ISmsNotificationProvider smsProvider,
    INotificationSenderConfigResolver senderConfigResolver,
    ILogger<ChannelNotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var channel = message.Channel.Trim().ToLowerInvariant();
        var sender = await senderConfigResolver.ResolveForTenantAsync(message.TenantId, cancellationToken);

        switch (channel)
        {
            case "email":
                if (string.IsNullOrWhiteSpace(message.RecipientKey))
                {
                    logger.LogDebug("Skipping email notification — empty recipient.");
                    return;
                }

                var fromEmail = sender.FromEmail;
                if (string.IsNullOrWhiteSpace(fromEmail))
                {
                    logger.LogWarning(
                        "Skipping email notification for tenant {TenantId} — no From address configured for {Country}.",
                        message.TenantId,
                        sender.CountryCode);
                    return;
                }

                await emailProvider.SendAsync(
                    new EmailNotificationRequest(
                        message.RecipientKey,
                        message.Subject,
                        message.Body,
                        fromEmail,
                        message.TenantId),
                    cancellationToken);
                return;

            case "sms" or "whatsapp":
                if (string.IsNullOrWhiteSpace(message.RecipientKey))
                {
                    logger.LogDebug("Skipping SMS notification — empty recipient.");
                    return;
                }

                var smsFrom = sender.SmsFrom;
                if (string.IsNullOrWhiteSpace(smsFrom))
                {
                    logger.LogWarning(
                        "Skipping SMS notification for tenant {TenantId} — no sender configured for {Country}.",
                        message.TenantId,
                        sender.CountryCode);
                    return;
                }

                await smsProvider.SendAsync(
                    new SmsNotificationRequest(
                        message.RecipientKey,
                        message.Body,
                        smsFrom,
                        message.TenantId),
                    cancellationToken);
                return;

            default:
                logger.LogWarning("Unknown notification channel {Channel}.", message.Channel);
                return;
        }
    }
}
