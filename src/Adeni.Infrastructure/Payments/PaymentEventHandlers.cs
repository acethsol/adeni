namespace Adeni.Infrastructure.Payments;

using Adeni.Application.Events;
using Adeni.Application.Notifications;
using Adeni.Domain.Booking;
using Adeni.Domain.Booking.Events;
using Adeni.Domain.Payments;
using Adeni.Domain.Payments.Events;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class PaymentBookingHandler(
    AdeniDbContext dbContext,
    IDomainEventCollector domainEventCollector) :
    IDomainEventHandler<PaymentCompleted>,
    IDomainEventHandler<PaymentFailed>,
    IDomainEventHandler<RefundInitiated>
{
    public async Task HandleAsync(PaymentCompleted domainEvent, CancellationToken cancellationToken = default)
    {
        if (!domainEvent.BookingId.HasValue || domainEvent.Type != PaymentIntentType.Deposit)
        {
            return;
        }

        var booking = await dbContext.Bookings
            .FirstOrDefaultAsync(
                x => x.Id == domainEvent.BookingId.Value && x.TenantId == domainEvent.TenantId,
                cancellationToken);

        if (booking is null || booking.Status != BookingStatus.Pending)
        {
            return;
        }

        booking.Status = BookingStatus.Confirmed;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        domainEventCollector.Add(new BookingConfirmed(
            booking.Id,
            booking.TenantId,
            booking.CustomerId,
            booking.StartAt,
            DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task HandleAsync(PaymentFailed domainEvent, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public async Task HandleAsync(RefundInitiated domainEvent, CancellationToken cancellationToken = default)
    {
        if (!domainEvent.BookingId.HasValue)
        {
            return;
        }

        var booking = await dbContext.Bookings
            .FirstOrDefaultAsync(
                x => x.Id == domainEvent.BookingId.Value && x.TenantId == domainEvent.TenantId,
                cancellationToken);

        if (booking is null || booking.Status is BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            return;
        }

        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTimeOffset.UtcNow;

        domainEventCollector.Add(new BookingCancelled(
            booking.Id,
            booking.TenantId,
            booking.CustomerId,
            booking.StartAt,
            DateTimeOffset.UtcNow));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

public sealed class PaymentNotificationHandler(INotificationDispatcher notificationDispatcher) :
    IDomainEventHandler<PaymentCompleted>,
    IDomainEventHandler<PaymentFailed>
{
    public Task HandleAsync(PaymentCompleted domainEvent, CancellationToken cancellationToken = default) =>
        notificationDispatcher.SendAsync(
            new NotificationMessage(
                "email",
                domainEvent.TenantId.ToString(),
                "Payment received",
                $"Payment {domainEvent.PaymentIntentId} of {domainEvent.Amount} {domainEvent.Currency} was confirmed.",
                domainEvent.TenantId),
            cancellationToken);

    public Task HandleAsync(PaymentFailed domainEvent, CancellationToken cancellationToken = default) =>
        notificationDispatcher.SendAsync(
            new NotificationMessage(
                "email",
                domainEvent.TenantId.ToString(),
                "Payment failed",
                $"Payment {domainEvent.PaymentIntentId} failed.{(string.IsNullOrWhiteSpace(domainEvent.Reason) ? "" : $" Reason: {domainEvent.Reason}")}",
                domainEvent.TenantId),
            cancellationToken);
}
