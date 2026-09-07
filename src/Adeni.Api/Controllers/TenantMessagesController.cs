namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Messaging;
using Adeni.Domain.Subscriptions;
using Adeni.Infrastructure.Auth;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/messages")]
public sealed class TenantMessagesController(
    IMessageThreadService messaging,
    AdeniDbContext dbContext,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet("threads")]
    public async Task<IActionResult> ListThreads(CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var tier = await ResolveTierAsync(tenantId, cancellationToken);
        if (tier is null)
        {
            return NotFound();
        }

        var result = await messaging.ListForTenantAsync(tenantId, tier.Value, cancellationToken);
        return ApiResults.FromResult(result, items => Ok(new { items }), HttpContext);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var tier = await ResolveTierAsync(tenantId, cancellationToken);
        if (tier is null)
        {
            return NotFound();
        }

        var result = await messaging.GetUnreadCountForTenantAsync(tenantId, tier.Value, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var tier = await ResolveTierAsync(tenantId, cancellationToken);
        if (tier is null)
        {
            return NotFound();
        }

        var result = await messaging.GetTemplatesForTenantAsync(tenantId, tier.Value, cancellationToken);
        return ApiResults.FromResult(result, items => Ok(new { items }), HttpContext);
    }

    [HttpGet("threads/{id:guid}")]
    public async Task<IActionResult> GetThread(
        Guid id,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is null)
        {
            return Unauthorized();
        }

        var result = await messaging.GetThreadAsync(
            ResolveAuth0Sub()!,
            MessageParticipantRole.Business,
            id,
            limit,
            cancellationToken);

        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("threads/{id:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid id,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null || ResolveTenantId() is null)
        {
            return Unauthorized();
        }

        var result = await messaging.SendMessageAsync(
            auth0Sub,
            MessageParticipantRole.Business,
            id,
            request,
            cancellationToken);

        return ApiResults.FromResult(
            result,
            payload => Created($"/api/v1/tenant/messages/threads/{id}/messages/{payload.Id}", payload),
            HttpContext);
    }

    [HttpPost("threads/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null || ResolveTenantId() is null)
        {
            return Unauthorized();
        }

        var result = await messaging.MarkReadAsync(
            auth0Sub,
            MessageParticipantRole.Business,
            id,
            cancellationToken);

        return ApiResults.FromResult(result, _ => NoContent(), HttpContext);
    }

    private async Task<SubscriptionTier?> ResolveTierAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        return tenant?.SubscriptionTier;
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
