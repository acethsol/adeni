namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Tenancy;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/tenant/settings/location-security")]
public sealed class TenantLocationSecurityController(ILocationSecurityPolicyService policies) : ControllerBase
{
    [HttpGet]
    [RequiresBusinessPermission(BusinessPermissions.Attendance, BusinessPermissions.AttendanceManage)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await policies.GetAsync(tenantId, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPut]
    [RequiresBusinessPermission(BusinessPermissions.AttendanceManage)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateLocationSecurityPolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var access = PortalAccessHttpContext.Get(HttpContext);
        if (access is null)
        {
            return Unauthorized();
        }

        var result = await policies.UpdateAsync(tenantId, access, request, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    private Guid? ResolveTenantId()
    {
        var tenantClaim = User.FindFirstValue(AdeniClaimTypes.TenantId);
        if (Guid.TryParse(tenantClaim, out var tenantId))
        {
            return tenantId;
        }

        if (Request.Headers.TryGetValue(TenantAccessMiddleware.TenantHeaderName, out var headerValue)
            && Guid.TryParse(headerValue, out tenantId))
        {
            return tenantId;
        }

        return null;
    }
}
