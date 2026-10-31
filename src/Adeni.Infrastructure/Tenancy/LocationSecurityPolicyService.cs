namespace Adeni.Infrastructure.Tenancy;

using Adeni.Application.Auth;
using Adeni.Application.Subscriptions;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class LocationSecurityPolicyService(
    AdeniDbContext dbContext,
    IEntitlementsService entitlementsService,
    TimeProvider clock) : ILocationSecurityPolicyService
{
    public const int MinValidityMinutes = 30;
    public const int MaxValidityMinutes = 180;

    public async Task<Result<LocationSecurityPolicyResponse>> GetAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<LocationSecurityPolicyResponse>(Error.NotFound("Tenant"));
        }

        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        var row = await dbContext.LocationSecurityPolicies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        return Result.Success(MapResponse(row, entitlements.OnPremisesSensitiveData));
    }

    public async Task<Result<LocationSecurityPolicyResponse>> UpdateAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        UpdateLocationSecurityPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!actor.Permissions.Contains(BusinessPermissions.AttendanceManage))
        {
            return Result.Failure<LocationSecurityPolicyResponse>(ErrorCodes.PermissionDeniedError());
        }

        var tenant = await dbContext.Tenants
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return Result.Failure<LocationSecurityPolicyResponse>(Error.NotFound("Tenant"));
        }

        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        if (!entitlements.OnPremisesSensitiveData)
        {
            return Result.Failure<LocationSecurityPolicyResponse>(ErrorCodes.AttendanceNotEntitledError());
        }

        var row = await dbContext.LocationSecurityPolicies
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (row is null)
        {
            row = new LocationSecurityPolicy { TenantId = tenantId };
            dbContext.LocationSecurityPolicies.Add(row);
        }

        row.RequirePresenceForSensitiveCustomerData = request.RequirePresenceForSensitiveCustomerData;
        if (request.PresenceValidityMinutes is int minutes)
        {
            row.PresenceValidityMinutes = Math.Clamp(minutes, MinValidityMinutes, MaxValidityMinutes);
        }

        if (request.AllowManagerPresenceOverride is bool allowOverride)
        {
            row.AllowManagerPresenceOverride = allowOverride;
        }

        row.UpdatedAt = clock.GetUtcNow();
        row.UpdatedByBusinessUserId = actor.BusinessUserId;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(MapResponse(row, entitlements.OnPremisesSensitiveData));
    }

    public async Task<(bool EnforcementActive, int ValidityMinutes)> ResolvePolicyAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return (false, VerifiedPresenceService.DefaultValidityMinutes);
        }

        var entitlements = entitlementsService.GetEntitlementsForTenant(tenant);
        if (!entitlements.OnPremisesSensitiveData)
        {
            return (false, VerifiedPresenceService.DefaultValidityMinutes);
        }

        var row = await dbContext.LocationSecurityPolicies.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (row is null || !row.RequirePresenceForSensitiveCustomerData)
        {
            return (false, row?.PresenceValidityMinutes ?? VerifiedPresenceService.DefaultValidityMinutes);
        }

        var validity = Math.Clamp(
            row.PresenceValidityMinutes,
            MinValidityMinutes,
            MaxValidityMinutes);
        return (true, validity);
    }

    private static LocationSecurityPolicyResponse MapResponse(
        LocationSecurityPolicy? row,
        bool onPremisesEntitled) =>
        new(
            row?.RequirePresenceForSensitiveCustomerData ?? false,
            Math.Clamp(row?.PresenceValidityMinutes ?? VerifiedPresenceService.DefaultValidityMinutes, MinValidityMinutes, MaxValidityMinutes),
            row?.AllowManagerPresenceOverride ?? true,
            onPremisesEntitled,
            onPremisesEntitled && (row?.RequirePresenceForSensitiveCustomerData ?? false));
}
