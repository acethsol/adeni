namespace Adeni.Api.Extensions;

using Adeni.Infrastructure.Notifications;
using Adeni.Infrastructure.Intake;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.Options;

public static class HangfireExtensions
{
    public static IServiceCollection AddAdeniHangfire(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (!ShouldRegisterHangfire(environment, configuration))
        {
            return services;
        }

        var connectionString = configuration.GetConnectionString("AdeniDb")!;

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true,
                SchemaName = "HangFire",
            }));

        services.AddHangfireServer();

        return services;
    }

    public static WebApplication UseAdeniHangfire(this WebApplication app)
    {
        if (!ShouldRegisterHangfire(app.Environment, app.Configuration))
        {
            return app;
        }

        if (app.Environment.IsDevelopment())
        {
            app.UseHangfireDashboard(
                "/admin/hangfire",
                new DashboardOptions
                {
                    Authorization = [new HangfireDevelopmentDashboardAuthorizationFilter()],
                });
        }

        app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<Adeni.Infrastructure.Calendar.CalendarReconciliationJob>("calendar-reconciliation-v1",
            job => job.ExecuteAsync(CancellationToken.None), "* * * * *");

        var reminders = app.Services.GetRequiredService<IOptions<NotificationsOptions>>().Value.Reminders;
        var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
        recurringJobs.AddOrUpdate<Adeni.Infrastructure.Finance.CommerceReconciliationJob>("commerce-reconciliation-v1",
            job => job.ExecuteAsync(CancellationToken.None), "*/5 * * * *");
        recurringJobs.AddOrUpdate<Adeni.Infrastructure.Retention.PackageExpiryHangfireJob>(
            Adeni.Infrastructure.Retention.PackageExpiryHangfireJob.RecurringJobId,
            job => job.ExecuteAsync(CancellationToken.None), "15 * * * *");
        recurringJobs.AddOrUpdate<Adeni.Infrastructure.Retention.MembershipBillingHangfireJob>(
            Adeni.Infrastructure.Retention.MembershipBillingHangfireJob.RecurringJobId,
            job => job.ExecuteAsync(CancellationToken.None), "30 * * * *");
        recurringJobs.AddOrUpdate<IntakeRetentionHangfireJob>("intake-retention-v1",
            job => job.ExecuteAsync(CancellationToken.None), "0 * * * *");

        if (reminders.Enabled)
        {
            recurringJobs.AddOrUpdate<AppointmentReminderHangfireJob>(
                AppointmentReminderHangfireJob.RecurringJobId,
                job => job.ExecuteAsync(CancellationToken.None),
                reminders.RecurringCron);
        }
        else
        {
            recurringJobs.RemoveIfExists(AppointmentReminderHangfireJob.RecurringJobId);
        }

        return app;
    }

    internal static bool ShouldRegisterHangfire(IHostEnvironment environment, IConfiguration configuration) =>
        HangfireNotificationsBootstrap.ShouldUseHangfire(environment, configuration);

    private sealed class HangfireDevelopmentDashboardAuthorizationFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
    {
        public bool Authorize(Hangfire.Dashboard.DashboardContext context) => true;
    }
}
