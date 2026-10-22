namespace Adeni.Application.Notifications;

using Adeni.Domain.Common;

public sealed record NotificationTemplateOverrideDto(
    string TemplateId,
    string Locale,
    string? Subject,
    string? Body);

public sealed record NotificationPreferencesResponse(
    bool EmailEnabled,
    bool PushEnabled,
    bool SmsRemindersEnabled,
    string DefaultNotificationLocale,
    string? SecondaryNotificationLocale,
    string NotificationLanguageMode,
    IReadOnlyList<NotificationTemplateOverrideDto> TemplateOverrides);

public sealed record UpdateNotificationPreferencesRequest(
    bool EmailEnabled,
    bool PushEnabled,
    bool SmsRemindersEnabled,
    string DefaultNotificationLocale,
    string? SecondaryNotificationLocale,
    string NotificationLanguageMode,
    IReadOnlyList<NotificationTemplateOverrideDto>? TemplateOverrides);

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
