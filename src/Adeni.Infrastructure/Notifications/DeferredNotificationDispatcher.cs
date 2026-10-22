namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Abstractions;
using Adeni.Application.Notifications;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Outermost <see cref="INotificationDispatcher"/> — after-commit enqueue (Hangfire or inline worker).
/// </summary>
public sealed class DeferredNotificationDispatcher(
    IAfterCommitWorkQueue afterCommitWorkQueue,
    IHostEnvironment environment,
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory) : INotificationDispatcher
{
    public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        var payload = NotificationJobPayload.FromMessage(message);
        if (HangfireNotificationsBootstrap.ShouldUseHangfire(environment, configuration))
        {
            return afterCommitWorkQueue.EnqueueAsync(
                _ =>
                {
                    BackgroundJob.Enqueue<NotificationSendHangfireJob>(
                        job => job.SendAsync(payload, CancellationToken.None));
                    return Task.CompletedTask;
                },
                cancellationToken);
        }

        return afterCommitWorkQueue.EnqueueAsync(
            async ct =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var worker = scope.ServiceProvider.GetRequiredService<NotificationSendWorker>();
                await worker.SendAsync(payload, ct);
            },
            cancellationToken);
    }
}
