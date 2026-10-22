namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Abstractions;
using Adeni.Application.Notifications;
using Hangfire;

public sealed class HangfireNotificationJobScheduler(IAfterCommitWorkQueue afterCommitWorkQueue)
    : INotificationJobScheduler
{
    public Task ScheduleSendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var payload = NotificationJobPayload.FromMessage(message);
        return afterCommitWorkQueue.EnqueueAsync(
            _ =>
            {
                BackgroundJob.Enqueue<NotificationSendHangfireJob>(
                    job => job.SendAsync(payload, CancellationToken.None));
                return Task.CompletedTask;
            },
            cancellationToken);
    }

    public Task SchedulePaymentReceiptAsync(
        PaymentReceiptNotificationJobArgs args,
        CancellationToken cancellationToken = default) =>
        afterCommitWorkQueue.EnqueueAsync(
            _ =>
            {
                BackgroundJob.Enqueue<NotificationSendHangfireJob>(
                    job => job.SendPaymentReceiptAsync(args, CancellationToken.None));
                return Task.CompletedTask;
            },
            cancellationToken);
}
