namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Events;
using Adeni.Application.Notifications;
using Adeni.Domain.Finance.Events;

public sealed class FinanceNotificationHandler(INotificationJobScheduler notificationJobs) :
    IDomainEventHandler<PaymentCompleted>,
    IDomainEventHandler<PaymentFailed>
{
    public Task HandleAsync(PaymentCompleted domainEvent, CancellationToken cancellationToken = default) =>
        notificationJobs.SchedulePaymentReceiptAsync(
            new PaymentReceiptNotificationJobArgs(
                domainEvent.PaymentIntentId,
                domainEvent.TenantId,
                domainEvent.BookingId,
                domainEvent.Amount,
                domainEvent.Currency),
            cancellationToken);

    public Task HandleAsync(PaymentFailed domainEvent, CancellationToken cancellationToken = default) =>
        notificationJobs.ScheduleSendAsync(
            new NotificationMessage(
                "email",
                domainEvent.TenantId.ToString(),
                "Payment failed",
                $"Payment {domainEvent.PaymentIntentId} failed.{(string.IsNullOrWhiteSpace(domainEvent.Reason) ? "" : $" Reason: {domainEvent.Reason}")}",
                domainEvent.TenantId,
                $"payment-failed:{domainEvent.PaymentIntentId:D}"),
            cancellationToken);
}
