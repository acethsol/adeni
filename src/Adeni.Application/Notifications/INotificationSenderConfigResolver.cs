namespace Adeni.Application.Notifications;

public sealed record ResolvedSenderConfig(string CountryCode, string? FromEmail, string? SmsFrom);

public interface INotificationSenderConfigResolver
{
    Task<ResolvedSenderConfig> ResolveForTenantAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
