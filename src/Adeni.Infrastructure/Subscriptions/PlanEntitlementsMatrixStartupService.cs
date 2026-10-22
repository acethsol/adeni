namespace Adeni.Infrastructure.Subscriptions;

using Adeni.Application.Subscriptions;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Eagerly loads plan-entitlements.json so <see cref="PlanEntitlementsCatalog"/> is initialized before HTTP traffic.
/// </summary>
internal sealed class PlanEntitlementsMatrixStartupService(IPlanEntitlementsMatrix matrix) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = matrix;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
