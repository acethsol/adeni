namespace Adeni.Api.Common;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public static class TenantListLocationQuery
{
    public static async Task<Result<Guid?>> ResolveAsync(
        AdeniDbContext dbContext,
        IEntitlementsService entitlementsService,
        Guid tenantId,
        Guid? locationId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<Guid?>(Error.NotFound("Business"));
        }

        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        return await TenantLocationScope.ResolveListLocationAsync(
            dbContext,
            tenantId,
            locationId,
            entitlements.MultiLocationReporting,
            cancellationToken);
    }
}
