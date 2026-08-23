namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Subscriptions;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/subscription")]
public sealed class TenantSubscriptionController(
    ISubscriptionService subscriptionService,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsage(CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await subscriptionService.GetTenantUsageAsync(tenantId.Value, auth0Sub, cancellationToken);
        return MapResult(result, Ok);
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
        ApiResults.FromResult(result, onSuccess, HttpContext);
}

[ApiController]
[Route("api/v1/subscriptions")]
public sealed class SubscriptionBillingController(
    ISubscriptionBillingProvider billingProvider) : ControllerBase
{
    /// <summary>Stub checkout — real Paystack integration deferred to Sprint 17.</summary>
    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckout(
        [FromBody] CreateSubscriptionCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var result = await billingProvider.CreateCheckoutAsync(request, cancellationToken);
        return result.Match<IActionResult>(
            Ok,
            error => BadRequest(new { title = error.Message }));
    }

    /// <summary>Stub webhook receiver — documents normalized event shape for future provider wiring.</summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        var headers = Request.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        var result = await billingProvider.ParseWebhookAsync(rawBody, headers, cancellationToken);
        return result.Match<IActionResult>(
            _ => Ok(new { received = true }),
            error => BadRequest(new { title = error.Message }));
    }
}
