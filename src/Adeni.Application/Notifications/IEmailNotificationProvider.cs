namespace Adeni.Application.Notifications;

public sealed record EmailNotificationRequest(
    string ToEmail,
    string Subject,
    string Body,
    string FromEmail,
    Guid? TenantId = null);

public interface IEmailNotificationProvider
{
    Task SendAsync(EmailNotificationRequest request, CancellationToken cancellationToken = default);
}
