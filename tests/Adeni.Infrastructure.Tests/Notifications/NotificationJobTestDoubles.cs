namespace Adeni.Infrastructure.Tests.Notifications;

using Adeni.Application.Notifications;

internal sealed class CapturingNotificationJobScheduler(INotificationDispatcher dispatcher) : INotificationJobScheduler
{
    public List<NotificationMessage> Messages { get; } = [];

    public Task ScheduleSendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        Messages.Add(message);
        return dispatcher.SendAsync(message, cancellationToken);
    }

    public Task SchedulePaymentReceiptAsync(
        PaymentReceiptNotificationJobArgs args,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Use integration setup with NotificationSendWorker for receipt jobs.");
}
