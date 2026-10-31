namespace Adeni.Application.Tenancy;

using Adeni.Application.Auth;
using Adeni.Domain.Common;

public sealed record LocationSecurityPolicyResponse(
    bool RequirePresenceForSensitiveCustomerData,
    int PresenceValidityMinutes,
    bool AllowManagerPresenceOverride,
    bool PolicyConfigurable,
    bool EnforcementActive);

public sealed record UpdateLocationSecurityPolicyRequest(
    bool RequirePresenceForSensitiveCustomerData,
    int? PresenceValidityMinutes = null,
    bool? AllowManagerPresenceOverride = null);

public interface ILocationSecurityPolicyService
{
    Task<Result<LocationSecurityPolicyResponse>> GetAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result<LocationSecurityPolicyResponse>> UpdateAsync(
        Guid tenantId,
        PortalAccessSnapshot actor,
        UpdateLocationSecurityPolicyRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool EnforcementActive, int ValidityMinutes)> ResolvePolicyAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
