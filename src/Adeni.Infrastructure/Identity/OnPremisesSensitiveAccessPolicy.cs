namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Auth;
using Adeni.Application.Identity;
using Adeni.Application.Reporting;
using Adeni.Application.Subscriptions;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class OnPremisesSensitiveAccessPolicy(
    AdeniDbContext dbContext,
    IEntitlementsService entitlementsService,
    ILocationSecurityPolicyService locationSecurityPolicyService,
    TimeProvider clock) : IOnPremisesSensitiveAccessPolicy
{
    private static readonly HashSet<string> SensitiveReportKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ReportKeys.TenantCustomers,
        ReportKeys.Compensation,
    };

    public bool IsSensitiveReportKey(string reportKey) =>
        SensitiveReportKeys.Contains(reportKey.Trim());

    public async Task<Result> EnsureSensitiveAccessAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        Guid? requiredLocationId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure(Error.NotFound("Tenant"));
        }

        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        if (!entitlements.OnPremisesSensitiveData)
        {
            return Result.Success();
        }

        var (enforcementActive, _) = await locationSecurityPolicyService.ResolvePolicyAsync(tenantId, cancellationToken);
        if (!enforcementActive)
        {
            return Result.Success();
        }

        var now = clock.GetUtcNow();
        var session = await dbContext.VerifiedPresenceSessions.AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.BusinessUserId == actor.BusinessUserId
                && x.InvalidatedAt == null
                && x.ExpiresAt > now)
            .OrderByDescending(x => x.VerifiedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return Result.Failure(ErrorCodes.StaffPresenceRequiredError());
        }

        if (session.ExpiresAt <= now)
        {
            return Result.Failure(ErrorCodes.StaffPresenceExpiredError());
        }

        if (requiredLocationId is Guid locationId && session.BusinessLocationId != locationId)
        {
            var canCrossBranch = entitlements.MultiLocationReporting
                && actor.Permissions.Contains(BusinessPermissions.Attendance);
            if (!canCrossBranch)
            {
                return Result.Failure(ErrorCodes.StaffPresenceLocationMismatchError());
            }
        }

        return Result.Success();
    }
}
