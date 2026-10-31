namespace Adeni.Api.Controllers;

using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/me/verification")]
public sealed class MeVerificationController(
    ICustomerTrustSyncService trustSync,
    ICustomerTrustQuery trustQuery,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        await trustSync.SyncAsync(auth0Sub, User, cancellationToken);
        var state = await trustQuery.GetByAuth0SubAsync(auth0Sub, cancellationToken);
        if (state is null)
        {
            return Ok(new CustomerVerificationResponse(false, false, null, null));
        }

        return Ok(new CustomerVerificationResponse(
            state.EmailVerified,
            state.PhoneVerified,
            state.EmailVerifiedAt,
            state.PhoneVerifiedAt));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        await trustSync.SyncAsync(auth0Sub, User, cancellationToken);
        return await Get(cancellationToken);
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
