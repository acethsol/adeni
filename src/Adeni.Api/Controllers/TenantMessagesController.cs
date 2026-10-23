namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Common;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Messaging;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/messages")]
[RequiresPortalPermission(PortalPermissions.Messages)]
public sealed class TenantMessagesController(
    IConversationService messaging,
    AdeniDbContext dbContext,
    IEntitlementsService entitlementsService,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    public sealed record SendMessageBody(
        string? Body = null,
        string? ClientMessageId = null,
        IReadOnlyList<SendMessageAttachmentRequest>? Attachments = null);

    [HttpGet("conversations")]
    public async Task<IActionResult> ListConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var scoped = await TenantListLocationQuery.ResolveAsync(
            dbContext,
            entitlementsService,
            tenantId,
            locationId,
            cancellationToken);
        if (scoped.IsFailure)
        {
            return ApiErrorResponseMapper.ToActionResult(scoped.Error!, HttpContext);
        }

        var result = await messaging.ListForTenantAsync(
            tenantId,
            page,
            pageSize,
            scoped.Value,
            cancellationToken);
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
    public async Task<IActionResult> GetUnreadCount(
        [FromQuery] Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var scoped = await TenantListLocationQuery.ResolveAsync(
            dbContext,
            entitlementsService,
            tenantId,
            locationId,
            cancellationToken);
        if (scoped.IsFailure)
        {
            return ApiErrorResponseMapper.ToActionResult(scoped.Error!, HttpContext);
        }

        var result = await messaging.GetTeamUnreadCountForTenantAsync(tenantId, scoped.Value, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await messaging.GetTemplatesForTenantAsync(tenantId, cancellationToken);
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
            new SendConversationMessageRequest(body.Body, body.ClientMessageId, body.Attachments),
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
