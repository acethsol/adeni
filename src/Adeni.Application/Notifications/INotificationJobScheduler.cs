namespace Adeni.Application.Notifications;

/// <summary>
/// Schedules outbound notification delivery (Hangfire when enabled; inline in Testing).
/// </summary>
public interface INotificationJobScheduler
{
    Task ScheduleSendAsync(NotificationMessage message, CancellationToken cancellationToken = default);

    Task SchedulePaymentReceiptAsync(
        PaymentReceiptNotificationJobArgs args,
        CancellationToken cancellationToken = default);
}
