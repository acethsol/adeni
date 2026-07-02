namespace Adeni.Infrastructure.Tenancy;

using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

internal static class VerifiedLocationQueries
{
    public static async Task<Guid> ResolveTenantIdBySlugAsync(
        AdeniDbContext dbContext,
        string normalizedSlug,
        CancellationToken cancellationToken)
    {
        var tenantId = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(location => location.IsActive && location.Slug == normalizedSlug)
            .Join(
                dbContext.Tenants.Where(tenant => tenant.Status == TenantStatus.Verified),
                location => location.TenantId,
                tenant => tenant.Id,
                (location, _) => location.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        return tenantId;
    }

    public static async Task<(Guid LocationId, Guid TenantId)?> ResolveLocationBySlugAsync(
        AdeniDbContext dbContext,
        string normalizedSlug,
        CancellationToken cancellationToken)
    {
        var match = await dbContext.BusinessLocations
            .AsNoTracking()
            .Where(location => location.IsActive && location.Slug == normalizedSlug)
            .Join(
                dbContext.Tenants.Where(tenant => tenant.Status == TenantStatus.Verified),
                location => location.TenantId,
                tenant => tenant.Id,
                (location, tenant) => new { LocationId = location.Id, TenantId = tenant.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (match is null)
        {
            return null;
        }

        return (match.LocationId, match.TenantId);
    }
}
