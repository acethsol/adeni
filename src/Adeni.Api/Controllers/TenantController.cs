namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Notifications;
using Adeni.Application.Tenancy;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant")]
public sealed class TenantController(
    IBusinessOnboardingService onboardingService,
    IBusinessLocationService locationService,
    INotificationPreferencesService notificationPreferences,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterBusinessRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await onboardingService.RegisterAsync(request, auth0Sub, cancellationToken);
        return MapResult(result, Ok);
    }

    [HttpGet("context")]
    public async Task<IActionResult> GetContext(CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await onboardingService.GetBusinessContextAsync(auth0Sub, cancellationToken);
        return MapResult(result, Ok);
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await onboardingService.GetProfileAsync(tenantId.Value, auth0Sub, cancellationToken);
        return MapResult(result, Ok);
    }

    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateBusinessProfileRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await onboardingService.UpdateProfileAsync(tenantId.Value, request, auth0Sub, cancellationToken);
        return MapResult(result, Ok);
    }

    [HttpPatch("settings")]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateBusinessSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await onboardingService.UpdateSettingsAsync(tenantId.Value, request, auth0Sub, cancellationToken);
        return MapResult(result, Ok);
    }

    [HttpGet("notification-preferences")]
    public async Task<IActionResult> GetNotificationPreferences(CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await notificationPreferences.GetAsync(tenantId.Value, auth0Sub, cancellationToken);
        return MapResult(result, Ok);
    }

    [HttpPatch("notification-preferences")]
    public async Task<IActionResult> UpdateNotificationPreferences(
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await notificationPreferences.UpdateAsync(tenantId.Value, request, auth0Sub, cancellationToken);
        return MapResult(result, Ok);
    }

    [HttpPost("verification")]
    public async Task<IActionResult> SubmitVerification(
        [FromBody] SubmitVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await onboardingService.SubmitVerificationAsync(tenantId.Value, request, auth0Sub, cancellationToken);
        return ApiResults.FromResult(result, () => NoContent(), HttpContext);
    }

    [HttpGet("locations")]
    public async Task<IActionResult> ListLocations(CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await locationService.ListAsync(tenantId.Value, auth0Sub, cancellationToken);
        return ApiResults.FromResult(result, locations => Ok(new { items = locations }), HttpContext);
    }

    [HttpPost("locations")]
    public async Task<IActionResult> AddLocation(
        [FromBody] UpsertBusinessLocationRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await locationService.AddAsync(tenantId.Value, request, auth0Sub, cancellationToken);
        return ApiResults.FromResult(result, location => Ok(location), HttpContext);
    }

    [HttpPatch("locations/{locationId:guid}")]
    public async Task<IActionResult> UpdateLocation(
        Guid locationId,
        [FromBody] UpsertBusinessLocationRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await locationService.UpdateAsync(tenantId.Value, locationId, request, auth0Sub, cancellationToken);
        return ApiResults.FromResult(result, location => Ok(location), HttpContext);
    }

    [HttpDelete("locations/{locationId:guid}")]
    public async Task<IActionResult> DeactivateLocation(
        Guid locationId,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        var tenantId = ResolveTenantId();
        if (auth0Sub is null || tenantId is null)
        {
            return Unauthorized();
        }

        var result = await locationService.DeactivateAsync(tenantId.Value, locationId, auth0Sub, cancellationToken);
        return ApiResults.FromResult(result, () => NoContent(), HttpContext);
    }

    private string? ResolveAuth0Sub()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return User.FindFirst("sub")?.Value;
        }

        if (!auth0Options.Value.Enabled
            && Request.Headers.TryGetValue(Middleware.DevBusinessAuthMiddleware.DevAuth0SubHeader, out var devSub)
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

    private IActionResult MapResult(Domain.Common.Result result, Func<IActionResult> onSuccess) =>
        ApiResults.FromResult(result, onSuccess, HttpContext);
}
