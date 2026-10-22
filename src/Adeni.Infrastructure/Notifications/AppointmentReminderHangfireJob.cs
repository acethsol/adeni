namespace Adeni.Infrastructure.Notifications;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>Hangfire entry point — delegates to <see cref="AppointmentReminderProcessor"/>.</summary>
public sealed class AppointmentReminderHangfireJob(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationsOptions> options,
    ILogger<AppointmentReminderHangfireJob> logger)
{
    public const string RecurringJobId = "notifications:appointment-reminders";

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Reminders.Enabled)
        {
            return;
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var processor = scope.ServiceProvider.GetRequiredService<AppointmentReminderProcessor>();
            await processor.ProcessDueRemindersAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Appointment reminder Hangfire job failed.");
            throw;
        }
    }
}
