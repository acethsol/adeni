namespace Adeni.Api.Controllers;

using Adeni.Api.Auth;
using Adeni.Application.Admin;
using Adeni.Application.Auth;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IAuthSyncService authSyncService,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(
        [FromBody] SyncAuthUserRequest request,
        CancellationToken cancellationToken)
    {
        var options = auth0Options.Value;
        string? authenticatedSub = null;

        if (options.Enabled)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            authenticatedSub = User.FindFirst("sub")?.Value;
        }

        var result = await authSyncService.SyncAsync(request, authenticatedSub, cancellationToken);

        return result.Match<IActionResult>(
            profile => Ok(profile),
            error => error.Code switch
            {
                "forbidden" => Forbid(),
                "validation" => BadRequest(new { title = error.Message }),
                _ => NotFound(new { title = error.Message })
            });
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        if (!auth0Options.Value.Enabled)
        {
            return StatusCode(
                StatusCodes.Status501NotImplemented,
                new { title = "Auth0 is disabled. Enable Auth0:Enabled to use this endpoint." });
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        var user = User.ToCurrentUser();
        return Ok(new AuthSessionResponse(
            user.UserId,
            user.Roles,
            user.TenantId?.Value,
            user.HasMfa));
    }
}

[ApiController]
[Route("api/v1/admin/businesses")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminBusinessesController(IAdminBusinessService adminBusinessService) : ControllerBase
{
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        var items = await adminBusinessService.GetPendingVerificationsAsync(cancellationToken);
        return Ok(new { items });
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await adminBusinessService.ApproveAsync(id, adminId, cancellationToken);

        return result.Match<IActionResult>(
            _ => NoContent(),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => NotFound(new { title = error.Message })
            });
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectBusinessRequest body,
        CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await adminBusinessService.RejectAsync(id, adminId, body.Reason, cancellationToken);

        return result.Match<IActionResult>(
            _ => NoContent(),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => NotFound(new { title = error.Message })
            });
    }
}

public sealed record RejectBusinessRequest(string Reason);
