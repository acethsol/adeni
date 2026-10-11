namespace Adeni.Infrastructure.Payments;

using Adeni.Application.Events;
using Adeni.Application.Notifications;
using Adeni.Application.Payments;
using Adeni.Domain.Booking;
using Adeni.Domain.Booking.Events;
using Adeni.Domain.Payments;
using Adeni.Domain.Payments.Events;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public sealed class PaymentBookingHandler(
    AdeniDbContext dbContext,
    IDomainEventCollector domainEventCollector,
    IBillingDocumentService billingDocuments,
    ILogger<PaymentBookingHandler> logger) :
    IDomainEventHandler<PaymentCompleted>,
    IDomainEventHandler<PaymentFailed>,
    IDomainEventHandler<RefundInitiated>
{
    public async Task HandleAsync(PaymentCompleted domainEvent, CancellationToken cancellationToken = default)
    {
        var receipt = await billingDocuments.IssueReceiptForPaymentAsync(
            domainEvent.PaymentIntentId,
            cancellationToken);
        if (receipt.IsFailure)
        {
            logger.LogWarning(
                "Could not issue receipt for payment {PaymentIntentId}: {Error}",
                domainEvent.PaymentIntentId,
                receipt.Error.Message);
        }

        if (!domainEvent.BookingId.HasValue)
        {
            return;
        }

        var booking = await dbContext.Bookings
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(
                x => x.Id == domainEvent.BookingId.Value && x.TenantId == domainEvent.TenantId,
                cancellationToken);

        if (booking is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        // Deposit success still auto-confirms pending bookings.
        if (domainEvent.Type == PaymentIntentType.Deposit && booking.Status == BookingStatus.Pending)
        {
            booking.Status = BookingStatus.Confirmed;
            booking.UpdatedAt = now;

            domainEventCollector.Add(new BookingConfirmed(
                booking.Id,
                booking.TenantId,
                booking.CustomerId,
                booking.StartAt,
                now));
        }

        // Balance / any booking-linked payment may close the floor loop.
        if (booking.Status is BookingStatus.Completed or BookingStatus.CheckedIn or BookingStatus.Confirmed)
        {
            var total = booking.Lines.Sum(x => x.PriceAmount);
            if (total <= 0)
            {
                var servicePrice = await dbContext.ServiceOfferings
                    .AsNoTracking()
                    .Where(x => x.Id == booking.ServiceOfferingId)
                    .Select(x => (decimal?)x.PriceAmount)
                    .FirstOrDefaultAsync(cancellationToken);
                total = servicePrice ?? 0m;
            }

            var paid = await dbContext.PaymentIntents
                .AsNoTracking()
                .Where(x =>
                    x.BookingId == booking.Id
                    && x.TenantId == booking.TenantId
                    && x.Status == PaymentIntentStatus.Completed)
                .SumAsync(x => x.Amount, cancellationToken);

            if (total > 0 && paid >= total)
            {
                booking.Status = BookingStatus.Paid;
                booking.SettlementSource = BookingSettlementSource.Online;
                booking.PaidAt = now;
                booking.UpdatedAt = now;
            }
        }

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
