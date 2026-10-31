namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Domain.Identity;
using Adeni.Application.Tenancy;
using Adeni.Application.Reviews;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/reviews")]
[RequiresBusinessPermission(BusinessPermissions.Profile)]
public sealed class TenantReviewsController(
    IReviewService reviews,
    ITenantLocationScopeService locationScope,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var scoped = await locationScope.ValidateOptionalLocationIdAsync(
            tenantId,
            locationId,
            cancellationToken);
        if (scoped.IsFailure)
        {
            return BadRequest(new { title = scoped.Error!.Message });
        }

        var result = await reviews.ListForTenantAsync(
            tenantId,
            page,
            pageSize,
            scoped.Value,
            cancellationToken);
        return result.Match<IActionResult>(
            value => Ok(new
            {
                items = value.Items,
                page = value.Page,
                pageSize = value.PageSize,
                totalCount = value.TotalCount,
                repliedCount = value.RepliedCount,
            }),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => BadRequest(new { title = error.Message }),
            });
    }

    [HttpPost("{id:guid}/reply")]
    public async Task<IActionResult> Reply(
        Guid id,
        [FromBody] ReplyToReviewRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await reviews.ReplyAsync(tenantId, auth0Sub, id, request, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
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
}
