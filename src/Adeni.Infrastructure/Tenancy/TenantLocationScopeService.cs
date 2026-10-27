namespace Adeni.Infrastructure.Tenancy;

using Adeni.Application.Subscriptions;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class TenantLocationScopeService(
    AdeniDbContext dbContext,
    IEntitlementsService entitlementsService) : ITenantLocationScopeService
{
    public async Task<Result<Guid?>> ResolveListLocationAsync(Guid tenantId, Guid? locationId, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<Guid?>(Error.NotFound("Business"));
        }

        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        return await TenantLocationScope.ResolveListLocationAsync(
            dbContext, tenantId, locationId, entitlements.MultiLocationReporting, cancellationToken);
    }

    public Task<Result<Guid?>> ValidateOptionalLocationIdAsync(Guid tenantId, Guid? locationId, CancellationToken cancellationToken = default) =>
        TenantLocationScope.ValidateOptionalLocationIdAsync(dbContext, tenantId, locationId, cancellationToken);

    public Task<Result<Guid>> ResolveAvailabilityLocationAsync(Guid tenantId, Guid? locationId, CancellationToken cancellationToken = default) =>
        TenantLocationScope.ResolveAvailabilityLocationAsync(dbContext, tenantId, locationId, cancellationToken);
}
