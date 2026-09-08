namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Microsoft.Extensions.Logging;

public sealed class PreferenceAwareNotificationDispatcher(
    INotificationPreferencesService preferencesService,
    LoggingNotificationDispatcher inner,
    ILogger<PreferenceAwareNotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (message.TenantId is { } tenantId)
        {
            var prefs = await preferencesService.GetOrDefaultsAsync(tenantId, cancellationToken);
            if (!IsChannelEnabled(message.Channel, prefs))
            {
                logger.LogDebug(
                    "Skipping notification [{Channel}] for tenant {TenantId} — channel disabled in preferences.",
                    message.Channel,
                    tenantId);
                return;
            }
        }

        await inner.SendAsync(message, cancellationToken);
    }

    private static bool IsChannelEnabled(string channel, NotificationPreferencesResponse prefs) =>
        channel.ToLowerInvariant() switch
        {
            "email" => prefs.EmailEnabled,
            "push" or "fcm" => prefs.PushEnabled,
            "sms" or "whatsapp" => prefs.SmsWhatsAppReminderEnabled,
            _ => true,
        };
}
