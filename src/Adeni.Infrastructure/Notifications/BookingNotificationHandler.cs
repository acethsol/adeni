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
            domainEvent.StartAt,
            $"booking-confirmed:{domainEvent.BookingId:D}",
            NotificationTemplateIds.BookingConfirmed,
            reason: null,
            cancellationToken);

    public Task HandleAsync(BookingRejected domainEvent, CancellationToken cancellationToken = default) =>
        SendBookingEmailAsync(
            domainEvent.TenantId,
            domainEvent.BookingId,
            domainEvent.CustomerId,
            startAt: null,
            $"booking-rejected:{domainEvent.BookingId:D}",
            NotificationTemplateIds.BookingRejected,
            domainEvent.Reason,
            cancellationToken);

    public Task HandleAsync(BookingCancelled domainEvent, CancellationToken cancellationToken = default) =>
        SendBookingEmailAsync(
            domainEvent.TenantId,
            domainEvent.BookingId,
            domainEvent.CustomerId,
            domainEvent.StartAt,
            $"booking-cancelled:{domainEvent.BookingId:D}",
            NotificationTemplateIds.BookingCancelled,
            reason: null,
            cancellationToken);

    private async Task SendBookingEmailAsync(
        Guid tenantId,
        Guid bookingId,
        Guid customerId,
        DateTimeOffset? startAt,
        string dedupeEventId,
        string templateId,
        string? reason,
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

        var data = new Dictionary<string, string>
        {
            ["bookingId"] = bookingId.ToString("D"),
        };

        if (startAt is not null)
        {
            data["startAtUtc"] = startAt.Value.ToString("o");
        }

        if (templateId == NotificationTemplateIds.BookingRejected && !string.IsNullOrWhiteSpace(reason))
        {
            data["reason"] = reason.Trim();
        }

        await notificationDispatcher.SendAsync(
            NotificationMessage.FromTemplate(
                "email",
                contact.Value.Email.Trim(),
                templateId,
                data,
                tenantId,
                dedupeEventId,
                customerId),
            cancellationToken);
    }
}
