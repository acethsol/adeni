namespace Adeni.Api.Controllers.Retention;

using Adeni.Api.Middleware;
using Adeni.Application.Retention;
using Adeni.Application.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

/// <summary>Signed-in customer's own packages (read-only) and memberships (read + self-service freeze / cancel).</summary>
[ApiController]
[Route("api/v1/me/retention")]
public sealed class MeRetentionController(
    ICustomerPackageService customerPackages,
    ICustomerMembershipService customerMemberships,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet("packages")]
    public async Task<IActionResult> MyPackages(CancellationToken cancellationToken)
    {
        var sub = ResolveCustomerAuth0Sub();
        if (sub is null)
        {
            return Unauthorized();
        }

        return ApiResults.FromResult(await customerPackages.ListForPlatformCustomerAsync(sub, cancellationToken), Ok, HttpContext);
    }

    /// <summary>Signed-in customer's memberships across businesses (Sprint 37.2-D); excludes unpaid pending checkouts.</summary>
    [HttpGet("memberships")]
    public async Task<IActionResult> MyMemberships(CancellationToken cancellationToken)
    {
        var sub = ResolveCustomerAuth0Sub();
        if (sub is null)
        {
            return Unauthorized();
        }

        return ApiResults.FromResult(await customerMemberships.ListForPlatformCustomerAsync(sub, cancellationToken), Ok, HttpContext);
    }

    /// <summary>Self-service freeze (only when the plan allows it). Always scoped to the caller's own memberships.</summary>
    [HttpPost("memberships/{id:guid}/freeze")]
    public async Task<IActionResult> Freeze(Guid id, CancellationToken cancellationToken)
    {
        var sub = ResolveCustomerAuth0Sub();
        return sub is null
            ? Unauthorized()
            : ApiResults.FromResult(await customerMemberships.FreezeForCustomerAsync(sub, id, cancellationToken), Ok, HttpContext);
    }

    [HttpPost("memberships/{id:guid}/unfreeze")]
    public async Task<IActionResult> Unfreeze(Guid id, CancellationToken cancellationToken)
    {
        var sub = ResolveCustomerAuth0Sub();
        return sub is null
            ? Unauthorized()
            : ApiResults.FromResult(await customerMemberships.UnfreezeForCustomerAsync(sub, id, cancellationToken), Ok, HttpContext);
    }

    /// <summary>Self-service cancel; applies the plan's cancellation policy (immediate, or end of the paid cycle).</summary>
    [HttpPost("memberships/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelMembershipRequest? request, CancellationToken cancellationToken)
    {
        var sub = ResolveCustomerAuth0Sub();
        if (sub is null)
        {
            return Unauthorized();
        }

        var result = await customerMemberships.CancelForCustomerAsync(sub, id, request ?? new CancelMembershipRequest(), cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    private string? ResolveCustomerAuth0Sub()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return User.FindFirst("sub")?.Value;
        }

        if (!auth0Options.Value.Enabled
            && Request.Headers.TryGetValue(DevCustomerAuthMiddleware.DevAuth0SubHeader, out var devSub)
            && !string.IsNullOrWhiteSpace(devSub))
        {
            return devSub.ToString();
        }

        return null;
    }
}