namespace Adeni.Application.Notifications;

public interface INotificationDispatcher
{
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}

public sealed record NotificationMessage(
    string Channel,
    string RecipientKey,
    string Subject,
    string Body,
    Guid? TenantId = null);
