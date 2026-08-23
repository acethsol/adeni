namespace Adeni.Infrastructure.Subscriptions;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class SubscriptionService(
    AdeniDbContext dbContext,
    IEntitlementsService entitlementsService) : ISubscriptionService
{
    public async Task<Result<SubscriptionUsageResponse>> GetTenantUsageAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        var access = await ResolveTenantAccessAsync(tenantId, auth0Sub, cancellationToken);
        if (access.IsFailure)
        {
            return Result.Failure<SubscriptionUsageResponse>(access.Error);
        }

        var usage = await entitlementsService.GetUsageAsync(
            tenantId,
            access.Value!.SubscriptionTier,
            cancellationToken);

        return Result.Success(usage);
    }

    private async Task<Result<Domain.Tenancy.Tenant>> ResolveTenantAccessAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Result.Failure<Domain.Tenancy.Tenant>(Error.Forbidden("Authentication is required."));
        }

        var hasAccess = await dbContext.BusinessUsers
            .AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Auth0Sub == auth0Sub, cancellationToken);

        if (!hasAccess)
        {
            return Result.Failure<Domain.Tenancy.Tenant>(Error.Forbidden("You do not have access to this business."));
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        return tenant is null
            ? Result.Failure<Domain.Tenancy.Tenant>(Error.NotFound("Business"))
            : Result.Success(tenant);
    }
}
