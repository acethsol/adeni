namespace Adeni.Infrastructure.Subscriptions;

using Adeni.Application.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>Seeds and caches the active pricing catalog on API startup (Development/Testing/in-memory).</summary>
public sealed class PricingCatalogStartupService(
    IServiceProvider serviceProvider,
    IHostEnvironment environment,
    ILogger<PricingCatalogStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() && environment.EnvironmentName != "Testing")
        {
            return;
        }

        try
        {
            using var scope = serviceProvider.CreateScope();
            var catalog = scope.ServiceProvider.GetRequiredService<IPricingCatalogService>();
            await catalog.EnsureSeededAsync(cancellationToken);
            await catalog.RefreshCacheAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pricing catalog startup seed/cache skipped.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
