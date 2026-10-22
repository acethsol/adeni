namespace Adeni.Infrastructure.Notifications;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

public static class HangfireNotificationsBootstrap
{
    public static bool ShouldUseHangfire(IHostEnvironment environment, IConfiguration configuration)
    {
        if (environment.EnvironmentName == "Testing")
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(configuration.GetConnectionString("AdeniDb"));
    }
}
