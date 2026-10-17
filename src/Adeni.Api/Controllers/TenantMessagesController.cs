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
    IConversationService messaging,
    AdeniDbContext dbContext,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    public sealed record SendMessageBody(string Body, string? ClientMessageId = null);

    [HttpGet("conversations")]
    public async Task<IActionResult> ListConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
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

        var result = await messaging.ListForTenantAsync(tenantId, tier.Value, page, pageSize, cancellationToken);
        return ApiResults.FromResult(
            result,
            payload => Ok(new
            {
                items = payload.Items,
                page = payload.Page,
                pageSize = payload.PageSize,
                totalCount = payload.TotalCount,
            }),
            HttpContext);
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

        var result = await messaging.GetTeamUnreadCountForTenantAsync(tenantId, tier.Value, cancellationToken);
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

    [HttpGet("conversations/{id:guid}")]
    public async Task<IActionResult> GetConversation(
        Guid id,
        [FromQuery] Guid? beforeId = null,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null || ResolveTenantId() is null)
        {
            return Unauthorized();
        }

        var result = await messaging.GetAsync(
            auth0Sub,
            MessagingActorRole.Business,
            id,
            beforeId,
            take,
            cancellationToken);

        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid id,
        [FromBody] SendMessageBody body,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null || ResolveTenantId() is null)
        {
            return Unauthorized();
        }

        var result = await messaging.SendAsync(
            auth0Sub,
            MessagingActorRole.Business,
            id,
            new SendConversationMessageRequest(body.Body, body.ClientMessageId),
            cancellationToken);

        return ApiResults.FromResult(
            result,
            payload => Created($"/api/v1/tenant/messages/conversations/{id}/messages/{payload.Id}", payload),
            HttpContext);
    }

    [HttpPost("conversations/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid id,
        [FromQuery] Guid? messageId = null,
        CancellationToken cancellationToken = default)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null || ResolveTenantId() is null)
        {
            return Unauthorized();
        }

        var result = await messaging.MarkReadAsync(
            auth0Sub,
            MessagingActorRole.Business,
            id,
            messageId,
            cancellationToken);

        return ApiResults.FromResult(result, () => NoContent(), HttpContext);
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetMessagingSettings(CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await messaging.GetMessagingSettingsAsync(tenantId, auth0Sub, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPatch("settings")]
    public async Task<IActionResult> UpdateMessagingSettings(
        [FromBody] UpdateMessagingSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await messaging.UpdateMessagingSettingsAsync(tenantId, request, auth0Sub, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
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
