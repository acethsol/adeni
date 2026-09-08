namespace Adeni.Application.Notifications;

using Adeni.Domain.Common;

public sealed record NotificationPreferencesResponse(
    bool EmailEnabled,
    bool PushEnabled,
    bool SmsWhatsAppReminderEnabled);

public sealed record UpdateNotificationPreferencesRequest(
    bool EmailEnabled,
    bool PushEnabled,
    bool SmsWhatsAppReminderEnabled);

public interface INotificationPreferencesService
{
    Task<Result<NotificationPreferencesResponse>> GetAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken = default);

    Task<Result<NotificationPreferencesResponse>> UpdateAsync(
        Guid tenantId,
        UpdateNotificationPreferencesRequest request,
        string auth0Sub,
        CancellationToken cancellationToken = default);

    Task<NotificationPreferencesResponse> GetOrDefaultsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
