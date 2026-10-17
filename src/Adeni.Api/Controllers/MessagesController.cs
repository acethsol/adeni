namespace Adeni.Api.Controllers;

using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Messaging;
using Adeni.Domain.Messaging;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/messages")]
public sealed class MessagesController(
    IConversationService messaging,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    public sealed record CreateConversationBody(
        Guid TenantId,
        string? ContextType = null,
        Guid? ContextId = null,
        Guid? BookingId = null);

    public sealed record SendMessageBody(
        string? Body = null,
        string? ClientMessageId = null,
        IReadOnlyList<SendMessageAttachmentRequest>? Attachments = null);

    [HttpPost("conversations")]
    public async Task<IActionResult> CreateConversation(
        [FromBody] CreateConversationBody body,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var contextType = ParseContextType(body.ContextType, body.ContextId ?? body.BookingId);
        var contextId = body.ContextId ?? body.BookingId;
        var result = await messaging.CreateOrGetAsync(
            auth0Sub,
            new CreateConversationRequest(body.TenantId, contextType, contextId),
            cancellationToken);

        return ApiResults.FromResult(
            result,
            payload => Created($"/api/v1/messages/conversations/{payload.Id}", payload),
            HttpContext);
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> ListConversations(CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var items = await messaging.ListForCustomerAsync(auth0Sub, cancellationToken);
        return Ok(new { items });
    }

    [HttpGet("conversations/{id:guid}")]
    public async Task<IActionResult> GetConversation(
        Guid id,
        [FromQuery] Guid? beforeId = null,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await messaging.GetAsync(
            auth0Sub,
            MessagingActorRole.Customer,
            id,
            beforeId,
            take,
            cancellationToken);

        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<IActionResult> ListMessages(
        Guid id,
        [FromQuery] Guid? beforeId = null,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default) =>
        await GetConversation(id, beforeId, take, cancellationToken);

    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid id,
        [FromBody] SendMessageBody body,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await messaging.SendAsync(
            auth0Sub,
            MessagingActorRole.Customer,
            id,
            new SendConversationMessageRequest(body.Body, body.ClientMessageId, body.Attachments),
            cancellationToken);

        return ApiResults.FromResult(
            result,
            payload => Created($"/api/v1/messages/conversations/{id}/messages/{payload.Id}", payload),
            HttpContext);
    }

    [HttpPost("conversations/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(
        Guid id,
        [FromQuery] Guid? messageId = null,
        CancellationToken cancellationToken = default)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await messaging.MarkReadAsync(
            auth0Sub,
            MessagingActorRole.Customer,
            id,
            messageId,
            cancellationToken);

        return ApiResults.FromResult(result, () => NoContent(), HttpContext);
    }

    private static ConversationContextType ParseContextType(string? raw, Guid? contextId)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return contextId is null ? ConversationContextType.Profile : ConversationContextType.Booking;
        }

        return Enum.TryParse<ConversationContextType>(raw, ignoreCase: true, out var parsed)
            ? parsed
            : ConversationContextType.Profile;
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
