namespace Adeni.Api.Controllers.Retention;

using Adeni.Api.Middleware;
using Adeni.Application.Retention;
using Adeni.Application.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

/// <summary>Signed-in customer's own packages and balances (read-only).</summary>
[ApiController]
[Route("api/v1/me/retention")]
public sealed class MeRetentionController(
    ICustomerPackageService customerPackages,
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