namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Api.Extensions;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/subscription")]
[RequiresBusinessPermission(BusinessPermissions.Overview, BusinessPermissions.Plan)]
public sealed class TenantSubscriptionController(
    ISubscriptionService subscriptionService,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsage(
        [FromQuery] Guid? locationId,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await subscriptionService.GetTenantUsageAsync(
            tenantId.Value,
            auth0Sub,
            locationId,
            cancellationToken);
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
[Route("api/v1/tenant/billing")]
[RequiresBusinessPermission(BusinessPermissions.Plan)]
public sealed class TenantBillingController(
    ISubscriptionBillingOrchestrator billingOrchestrator,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListCharges(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await billingOrchestrator.ListChargesForTenantAsync(
            tenantId.Value,
            auth0Sub,
            limit,
            cancellationToken);

        return result.Match<IActionResult>(
            items => Ok(new { items }),
            error => ApiErrorResponseMapper.ToActionResult(error, HttpContext));
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

[ApiController]
[Route("api/v1/subscriptions")]
public sealed class SubscriptionBillingController(
    ISubscriptionBillingProvider billingProvider,
    ISubscriptionBillingOrchestrator billingOrchestrator,
    IOptions<Auth0Options> auth0Options,
    IHostEnvironment environment) : ControllerBase
{
    [HttpPost("checkout")]
    [RequiresBusinessPermission(BusinessPermissions.Plan)]
    public async Task<IActionResult> CreateCheckout(
        [FromBody] CreateSubscriptionCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (ResolveAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        if (request.TenantId != tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { title = "Cross-tenant access denied." });
        }

        var result = await billingProvider.CreateCheckoutAsync(request, cancellationToken);
        return result.Match<IActionResult>(
            Ok,
            error => BadRequest(new { title = error.Message }));
    }

    /// <summary>Stub/dev: finalize checkout when provider webhook is not wired (Development/Testing only).</summary>
    [HttpPost("checkout/complete")]
    [RequiresBusinessPermission(BusinessPermissions.Plan)]
    public async Task<IActionResult> CompleteStubCheckout(
        [FromBody] CompleteStubSubscriptionCheckoutRequest body,
        CancellationToken cancellationToken)
    {
        if (!IsDevOrTesting())
        {
            return NotFound();
        }

        if (ResolveAuth0Sub() is not { } auth0Sub || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        if (body.TenantId != tenantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { title = "Cross-tenant access denied." });
        }

        var result = await billingOrchestrator.CompleteStubCheckoutAsync(
            tenantId,
            auth0Sub,
            body.ProviderReference,
            cancellationToken);

        return result.Match<IActionResult>(
            Ok,
            error => ApiErrorResponseMapper.ToActionResult(error, HttpContext));
    }

    [HttpPost("webhook")]
    [EnableRateLimiting(RateLimitingExtensions.WebhookPolicy)]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        var headers = Request.Headers.ToDictionary(
            h => h.Key,
            h => h.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        await billingOrchestrator.RecordWebhookReceivedAsync(cancellationToken);

        var parseResult = await billingProvider.ParseWebhookAsync(rawBody, headers, cancellationToken);
        if (parseResult.IsFailure)
        {
            return ApiErrorResponseMapper.ToActionResult(parseResult.Error, HttpContext);
        }

        var applyResult = await billingOrchestrator.ApplyWebhookEventAsync(parseResult.Value!, cancellationToken);
        return applyResult.Match<IActionResult>(
            _ => Ok(new { received = true }),
            error => ApiErrorResponseMapper.ToActionResult(error, HttpContext));
    }

    private string? ResolveAuth0Sub()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return User.FindFirst("sub")?.Value;
        }

        if (!auth0Options.Value.Enabled
            && IsDevOrTesting()
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

    private bool IsDevOrTesting() =>
        environment.IsDevelopment() || environment.EnvironmentName == "Testing";
}

public sealed record CompleteStubSubscriptionCheckoutRequest(Guid TenantId, string ProviderReference);
