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
        Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Result.Failure<SubscriptionUsageResponse>(Error.Forbidden("Authentication is required."));
        }

        var hasAccess = await dbContext.BusinessUsers
            .AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Auth0Sub == auth0Sub, cancellationToken);

        if (!hasAccess)
        {
            return Result.Failure<SubscriptionUsageResponse>(Error.Forbidden("You do not have access to this business."));
        }

        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<SubscriptionUsageResponse>(Error.NotFound("Business"));
        }

        var locationCheck = await TenantLocationScope.ValidateOptionalLocationIdAsync(
            dbContext,
            tenantId,
            locationId,
            cancellationToken);
        if (locationCheck.IsFailure)
        {
            return Result.Failure<SubscriptionUsageResponse>(locationCheck.Error!);
        }

        if (TenantBillingLifecycle.ApplyPendingTransitions(tenant, DateTimeOffset.UtcNow))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var usage = await entitlementsService.GetUsageForTenantAsync(
            tenant,
            locationCheck.Value,
            cancellationToken);

        return Result.Success(usage);
    }
}
