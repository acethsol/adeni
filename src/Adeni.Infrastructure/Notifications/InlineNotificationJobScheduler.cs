namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Abstractions;
using Adeni.Application.Notifications;
using Microsoft.Extensions.DependencyInjection;

public sealed class InlineNotificationJobScheduler(
    IAfterCommitWorkQueue afterCommitWorkQueue,
    IServiceScopeFactory scopeFactory) : INotificationJobScheduler
{
    public Task ScheduleSendAsync(NotificationMessage message, CancellationToken cancellationToken = default) =>
        afterCommitWorkQueue.EnqueueAsync(
            async ct =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var worker = scope.ServiceProvider.GetRequiredService<NotificationSendWorker>();
                await worker.SendAsync(NotificationJobPayload.FromMessage(message), ct);
            },
            cancellationToken);

    public Task SchedulePaymentReceiptAsync(
        PaymentReceiptNotificationJobArgs args,
        CancellationToken cancellationToken = default) =>
        afterCommitWorkQueue.EnqueueAsync(
            async ct =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var sender = scope.ServiceProvider.GetRequiredService<PaymentReceiptNotificationSender>();
                await sender.SendAsync(args, ct);
            },
            cancellationToken);
}
