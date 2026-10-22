namespace Adeni.Application.Notifications;

using Adeni.Domain.Common;

public sealed record NotificationContact(string? Email, string? PhoneE164);

public interface INotificationRecipientResolver
{
    Task<Result<NotificationContact>> ResolvePlatformCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<Result<NotificationContact>> ResolveBookingCustomerAsync(
        Guid tenantId,
        Guid bookingId,
        CancellationToken cancellationToken = default);
}
