namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Messaging;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/messages")]
public sealed class MessagesController(
    IMessageThreadService messaging,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpPost("threads")]
    public async Task<IActionResult> CreateThread(
        [FromBody] CreateThreadRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await messaging.CreateOrGetThreadAsync(auth0Sub, request, cancellationToken);
        return ApiResults.FromResult(
            result,
            payload => Created($"/api/v1/messages/threads/{payload.Id}", payload),
            HttpContext);
    }

    [HttpGet("threads")]
    public async Task<IActionResult> ListThreads(CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var items = await messaging.ListForCustomerAsync(auth0Sub, cancellationToken);
        return Ok(new { items });
    }

    [HttpGet("threads/{id:guid}")]
    public async Task<IActionResult> GetThread(
        Guid id,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await messaging.GetThreadAsync(
            auth0Sub,
            MessageParticipantRole.Customer,
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
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await messaging.SendMessageAsync(
            auth0Sub,
            MessageParticipantRole.Customer,
            id,
            request,
            cancellationToken);

        return ApiResults.FromResult(
            result,
            payload => Created($"/api/v1/messages/threads/{id}/messages/{payload.Id}", payload),
            HttpContext);
    }

    [HttpPost("threads/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await messaging.MarkReadAsync(
            auth0Sub,
            MessageParticipantRole.Customer,
            id,
            cancellationToken);

        return ApiResults.FromResult(result, _ => NoContent(), HttpContext);
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
