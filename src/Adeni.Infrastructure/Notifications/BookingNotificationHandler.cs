namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Events;
using Adeni.Application.Notifications;
using Adeni.Domain.Booking.Events;
using Microsoft.Extensions.Logging;

public sealed class BookingNotificationHandler(
    INotificationDispatcher notificationDispatcher,
    INotificationRecipientResolver recipientResolver,
    ILogger<BookingNotificationHandler> logger)
    : IDomainEventHandler<BookingConfirmed>,
      IDomainEventHandler<BookingRejected>,
      IDomainEventHandler<BookingCancelled>
{
    public Task HandleAsync(BookingConfirmed domainEvent, CancellationToken cancellationToken = default) =>
        SendBookingEmailAsync(
            domainEvent.TenantId,
            domainEvent.BookingId,
            domainEvent.CustomerId,
            $"booking-confirmed:{domainEvent.BookingId:D}",
            "Booking confirmed",
            $"Your booking {domainEvent.BookingId} at {domainEvent.StartAt:u} has been confirmed.",
            cancellationToken);

    public Task HandleAsync(BookingRejected domainEvent, CancellationToken cancellationToken = default) =>
        SendBookingEmailAsync(
            domainEvent.TenantId,
            domainEvent.BookingId,
            domainEvent.CustomerId,
            $"booking-rejected:{domainEvent.BookingId:D}",
            "Booking declined",
            string.IsNullOrWhiteSpace(domainEvent.Reason)
                ? $"Your booking {domainEvent.BookingId} was declined."
                : $"Your booking {domainEvent.BookingId} was declined: {domainEvent.Reason}",
            cancellationToken);

    public Task HandleAsync(BookingCancelled domainEvent, CancellationToken cancellationToken = default) =>
        SendBookingEmailAsync(
            domainEvent.TenantId,
            domainEvent.BookingId,
            domainEvent.CustomerId,
            $"booking-cancelled:{domainEvent.BookingId:D}",
            "Booking cancelled",
            $"Your booking {domainEvent.BookingId} scheduled for {domainEvent.StartAt:u} was cancelled.",
            cancellationToken);

    private async Task SendBookingEmailAsync(
        Guid tenantId,
        Guid bookingId,
        Guid customerId,
        string dedupeEventId,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var contact = await recipientResolver.ResolveBookingCustomerAsync(tenantId, bookingId, cancellationToken);
        if (contact.IsFailure)
        {
            contact = await recipientResolver.ResolvePlatformCustomerAsync(customerId, cancellationToken);
        }

        if (contact.IsFailure || string.IsNullOrWhiteSpace(contact.Value!.Email))
        {
            logger.LogDebug(
                "Skipping booking email for booking {BookingId} — customer contact not resolved.",
                bookingId);
            return;
        }

        await notificationDispatcher.SendAsync(
            new NotificationMessage(
                "email",
                contact.Value.Email.Trim(),
                subject,
                body,
                tenantId,
                dedupeEventId),
            cancellationToken);
    }
}
