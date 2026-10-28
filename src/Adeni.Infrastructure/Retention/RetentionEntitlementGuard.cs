namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Retention;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class RetentionEntitlementGuard(
    AdeniDbContext db,
    IEntitlementsService entitlements) : IRetentionEntitlementGuard
{
    public async Task<bool> IsPackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        return tenant is not null && entitlements.GetEntitlementsForTenant(tenant).Packages;
    }

    public async Task<Result> EnsurePackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await IsPackagesEntitledAsync(tenantId, cancellationToken)
            ? Result.Success()
            : Result.Failure(RetentionErrorCodes.PackagesNotEntitledError());
}