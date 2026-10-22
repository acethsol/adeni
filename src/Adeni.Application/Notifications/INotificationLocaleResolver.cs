namespace Adeni.Application.Notifications;

public interface INotificationLocaleResolver
{
    Task<string> ResolveAsync(
        Guid? tenantId,
        Guid? customerId,
        string? localeHint,
        CancellationToken cancellationToken = default);
}
