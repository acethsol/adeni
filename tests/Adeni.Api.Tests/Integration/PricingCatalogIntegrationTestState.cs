namespace Adeni.Api.Tests.Integration;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Subscriptions;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class PricingCatalogIntegrationTestState
{
    /// <summary>
    /// Shared in-memory DB (<c>adeni-Testing</c>) is mutated by admin catalog tests; reset launch as active.
    /// </summary>
    public static async Task EnsureLaunchCatalogActiveAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var catalog = scope.ServiceProvider.GetRequiredService<IPricingCatalogService>();

        var launchCode = PricingCatalogMetadata.ActiveVersionCode;
        var versions = await db.PricingCatalogVersions.ToListAsync();
        foreach (var version in versions)
        {
            if (version.Code == launchCode)
            {
                version.Status = PricingCatalogVersionStatus.Active;
            }
            else if (version.Status == PricingCatalogVersionStatus.Active)
            {
                version.Status = PricingCatalogVersionStatus.Retired;
            }
        }

        await db.SaveChangesAsync();
        await catalog.RefreshCacheAsync();
    }
}
