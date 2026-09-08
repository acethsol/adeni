namespace Adeni.Api.Controllers;

using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Storage;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/customer/media")]
public sealed class CustomerMediaController(
    ICustomerMediaService mediaService,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpPost("upload-url")]
    public async Task<IActionResult> CreateUploadUrl(
        [FromBody] MediaUploadUrlRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await mediaService.CreateQuotePhotoUploadUrlAsync(auth0Sub, request, cancellationToken);
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
