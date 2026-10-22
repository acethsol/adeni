namespace Adeni.Application.Notifications;

public sealed record SmsNotificationRequest(
    string ToPhoneE164,
    string Body,
    string FromPhoneOrSenderId,
    Guid? TenantId = null);

public interface ISmsNotificationProvider
{
    Task SendAsync(SmsNotificationRequest request, CancellationToken cancellationToken = default);
}
