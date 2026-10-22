namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Events;
using Adeni.Application.Notifications;
using Adeni.Domain.Finance.Events;

public sealed class FinanceNotificationHandler(INotificationDispatcher notificationDispatcher) :
    IDomainEventHandler<PaymentCompleted>,
    IDomainEventHandler<PaymentFailed>
{
    public Task HandleAsync(PaymentCompleted domainEvent, CancellationToken cancellationToken = default) =>
        notificationDispatcher.SendAsync(
            new NotificationMessage(
                Channel: "email",
                RecipientKey: string.Empty,
                Subject: string.Empty,
                Body: string.Empty,
                TenantId: domainEvent.TenantId,
                DedupeEventId: $"payment-receipt:{domainEvent.PaymentIntentId:D}",
                PaymentReceipt: new PaymentReceiptNotificationJobArgs(
                    domainEvent.PaymentIntentId,
                    domainEvent.TenantId,
                    domainEvent.BookingId,
                    domainEvent.Amount,
                    domainEvent.Currency)),
            cancellationToken);

    public Task HandleAsync(PaymentFailed domainEvent, CancellationToken cancellationToken = default) =>
        notificationDispatcher.SendAsync(
            new NotificationMessage(
                "email",
                domainEvent.TenantId.ToString(),
                "Payment failed",
                $"Payment {domainEvent.PaymentIntentId} failed.{(string.IsNullOrWhiteSpace(domainEvent.Reason) ? "" : $" Reason: {domainEvent.Reason}")}",
                domainEvent.TenantId,
                $"payment-failed:{domainEvent.PaymentIntentId:D}"),
            cancellationToken);
}
