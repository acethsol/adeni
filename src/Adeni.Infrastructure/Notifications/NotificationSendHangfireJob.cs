namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>Hangfire entry point for transactional notification sends.</summary>
public sealed class NotificationSendHangfireJob(
    IServiceScopeFactory scopeFactory,
    ILogger<NotificationSendHangfireJob> logger)
{
    [AutomaticRetry(Attempts = 5, DelaysInSeconds = [60, 120, 300, 600, 900])]
    public async Task SendAsync(NotificationJobPayload payload, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var worker = scope.ServiceProvider.GetRequiredService<NotificationSendWorker>();
            await worker.SendAsync(payload, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Notification Hangfire job failed dedupe={DedupeEventId}",
                payload.DedupeEventId);
            throw;
        }
    }

}
