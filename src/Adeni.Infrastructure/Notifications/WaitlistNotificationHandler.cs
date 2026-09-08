namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Events;
using Adeni.Application.Notifications;
using Adeni.Domain.Booking.Events;
using Microsoft.Extensions.Logging;

public sealed class WaitlistNotificationHandler(
    INotificationDispatcher notificationDispatcher,
    ILogger<WaitlistNotificationHandler> logger) : IDomainEventHandler<SlotOpened>
{
    public Task HandleAsync(SlotOpened domainEvent, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Waitlist stub: slot opened for tenant {TenantId} service {ServiceId} at {StartAt}",
            domainEvent.TenantId,
            domainEvent.ServiceOfferingId,
            domainEvent.StartAt);

        return notificationDispatcher.SendAsync(
            new NotificationMessage(
                "email",
                domainEvent.TenantId.ToString(),
                "Waitlist slot available",
                $"A slot opened at {domainEvent.StartAt:u} for service {domainEvent.ServiceOfferingId}.",
                domainEvent.TenantId),
            cancellationToken);
    }
}
