namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Events;
using Adeni.Application.Notifications;
using Adeni.Domain.Booking.Events;

public sealed class BookingNotificationHandler(INotificationDispatcher notificationDispatcher)
    : IDomainEventHandler<BookingConfirmed>,
      IDomainEventHandler<BookingRejected>,
      IDomainEventHandler<BookingCancelled>
{
    public Task HandleAsync(BookingConfirmed domainEvent, CancellationToken cancellationToken = default) =>
        notificationDispatcher.SendAsync(
            new NotificationMessage(
                "email",
                domainEvent.CustomerId.ToString(),
                "Booking confirmed",
                $"Your booking {domainEvent.BookingId} at {domainEvent.StartAt:u} has been confirmed."),
            cancellationToken);

    public Task HandleAsync(BookingRejected domainEvent, CancellationToken cancellationToken = default) =>
        notificationDispatcher.SendAsync(
            new NotificationMessage(
                "email",
                domainEvent.CustomerId.ToString(),
                "Booking declined",
                string.IsNullOrWhiteSpace(domainEvent.Reason)
                    ? $"Your booking {domainEvent.BookingId} was declined."
                    : $"Your booking {domainEvent.BookingId} was declined: {domainEvent.Reason}"),
            cancellationToken);

    public Task HandleAsync(BookingCancelled domainEvent, CancellationToken cancellationToken = default) =>
        notificationDispatcher.SendAsync(
            new NotificationMessage(
                "email",
                domainEvent.CustomerId.ToString(),
                "Booking cancelled",
                $"Your booking {domainEvent.BookingId} scheduled for {domainEvent.StartAt:u} was cancelled."),
            cancellationToken);
}
