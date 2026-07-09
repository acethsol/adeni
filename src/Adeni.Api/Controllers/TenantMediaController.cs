namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Storage;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant")]
public sealed class TenantMediaController(
    ITenantMediaService mediaService,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpPost("media/upload-url")]
    public async Task<IActionResult> CreateUploadUrl(
        [FromBody] MediaUploadUrlRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await mediaService.CreateUploadUrlAsync(tenantId.Value, auth0Sub, request, cancellationToken);
        return MapResult(result, Ok);
    }

    [HttpPatch("profile/cover")]
    public async Task<IActionResult> UpdateCover(
        [FromBody] UpdateCoverImageRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await mediaService.UpdateCoverImageAsync(tenantId.Value, auth0Sub, request, cancellationToken);
        return MapResult(result, coverImageUrl => Ok(new { coverImageUrl }));
    }

    private string? ResolveAuth0Sub()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return User.FindFirst("sub")?.Value;
        }

        if (!auth0Options.Value.Enabled
            && Request.Headers.TryGetValue(DevBusinessAuthMiddleware.DevAuth0SubHeader, out var devSub)
            && !string.IsNullOrWhiteSpace(devSub))
        {
            return devSub.ToString();
        }

        return null;
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

    private IActionResult MapResult<T>(Domain.Common.Result<T> result, Func<T, IActionResult> onSuccess) =>
        result.Match<IActionResult>(
            onSuccess,
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                "conflict" => Conflict(new { title = error.Message }),
                "forbidden" => Forbid(),
                _ => NotFound(new { title = error.Message })
            });
}
