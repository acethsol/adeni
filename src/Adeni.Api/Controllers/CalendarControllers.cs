namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Extensions;
using Adeni.Api.Middleware;
using Adeni.Application.Calendar;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Calendar;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

public abstract class CalendarControllerBase : ControllerBase
{
    protected string? Actor => User.Identity?.IsAuthenticated == true ? User.FindFirstValue("sub") : null;
    protected Guid? Tenant => Guid.TryParse(Request.Headers[TenantAccessMiddleware.TenantHeaderName], out var id) ? id : null;
    protected IActionResult Map<T>(Result<T> result) => ApiResults.FromResult(result, value => Ok(value), HttpContext);
    protected void PrivateResponse()
    {
        Response.Headers.CacheControl = "no-store, private"; Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }
}

[ApiController, Route("api/v1/tenant/calendar"), RequiresPortalPermission(PortalPermissions.Staff)]
public sealed class CalendarManagementController(ICalendarService calendar) : CalendarControllerBase
{
    [HttpGet("connections")]
    public async Task<IActionResult> List(CancellationToken ct) { PrivateResponse(); return Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await calendar.ListAsync(tenant, actor, ct)); }
    [HttpPost("connections"), RequestSizeLimit(2048)]
    public async Task<IActionResult> Connect(CalendarConnectRequest request, CancellationToken ct) { PrivateResponse(); return Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await calendar.ConnectAsync(tenant, actor, request, ct)); }
    [HttpGet("connections/{id:guid}/calendars")]
    public async Task<IActionResult> Calendars(Guid id, CancellationToken ct) { PrivateResponse(); return Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await calendar.CalendarsAsync(tenant, actor, id, ct)); }
    [HttpPut("connections/{id:guid}/selection"), RequestSizeLimit(16384)]
    public async Task<IActionResult> Select(Guid id, CalendarSelectionRequest request, CancellationToken ct) { PrivateResponse(); return Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await calendar.SelectAsync(tenant, actor, id, request, ct)); }
    [HttpPost("connections/{id:guid}/disconnect")]
    public async Task<IActionResult> Disconnect(Guid id, CancellationToken ct) { PrivateResponse(); return Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await calendar.DisconnectAsync(tenant, actor, id, ct)); }
    [HttpPost("connections/{id:guid}/sync"), EnableRateLimiting(RateLimitingExtensions.PublicMutationPolicy)]
    public async Task<IActionResult> Sync(Guid id, CancellationToken ct) { PrivateResponse(); return Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await calendar.SyncAsync(tenant, actor, id, ct)); }
    [HttpGet("conflicts")]
    public async Task<IActionResult> Conflicts(CancellationToken ct) { PrivateResponse(); return Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await calendar.ConflictsAsync(tenant, actor, ct)); }
    [HttpPost("conflicts/{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct) { PrivateResponse(); return Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await calendar.AcknowledgeAsync(tenant, actor, id, ct)); }
}

[ApiController, Route("api/v1/calendar/oauth"), EnableRateLimiting(RateLimitingExtensions.PublicMutationPolicy)]
public sealed class CalendarOAuthController(ICalendarService calendar, IOptions<CalendarOptions> options) : CalendarControllerBase
{
    private const string Cookie = "__Host-adeni-calendar";
    [HttpGet("launch")]
    public async Task<IActionResult> Launch([FromQuery] string? ticket, CancellationToken ct)
    {
        PrivateResponse(); if (Request.Query.Count != 1 || Request.Query["ticket"].Count != 1 || !Request.IsHttps) return BadRequest(new { code = "calendar.authorization_invalid" });
        var result = await calendar.LaunchAsync(ticket ?? "", ct);
        if (result.IsFailure) return Map(result);
        Response.Cookies.Append(Cookie, result.Value!.Correlation, new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/", MaxAge = TimeSpan.FromMinutes(10), IsEssential = true });
        return Redirect(result.Value.AuthorizationUrl);
    }
    [HttpGet("{provider}/callback")]
    public async Task<IActionResult> Callback(string provider, [FromQuery] string? state, [FromQuery] string? code, CancellationToken ct)
    {
        PrivateResponse();
        var proof = Request.Cookies[Cookie] ?? "";
        Response.Cookies.Delete(Cookie, new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/" });
        var valid = Request.IsHttps && Request.Query["state"].Count == 1 && Request.Query["code"].Count == 1;
        var result = valid ? await calendar.CallbackAsync(provider, state ?? "", proof, code ?? "", ct) : Result.Failure<bool>(new("calendar.authorization_invalid"));
        // Fixed configured destination, never reflect state/code/provider errors into a public URL.
        return Redirect(options.Value.PortalBaseUrl.TrimEnd('/') + "/settings/calendars?calendar=" + (result.IsSuccess ? "connected" : "failed"));
    }
}

[ApiController, Route("api/v1/tenant/booking-links"), RequiresPortalPermission(PortalPermissions.PublicPage)]
public sealed class BookingLinkManagementController(IBookingLinkService links) : CalendarControllerBase
{
    [HttpPost, RequestSizeLimit(2048)]
    public async Task<IActionResult> Publish(BookingLinkRequest request, CancellationToken ct) { PrivateResponse(); return Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await links.PublishAsync(tenant, actor, request, ct)); }
}

[ApiController, Route("api/v1/public"), EnableRateLimiting(RateLimitingExtensions.PublicReadPolicy)]
public sealed class PublicBookingLinksController(IBookingLinkService links) : CalendarControllerBase
{
    private bool Selection(out Guid? service, out Guid? practitioner)
    {
        service = null; practitioner = null;
        foreach (var pair in Request.Query)
        {
            if (pair.Key is not ("service" or "practitioner") || pair.Value.Count != 1 || !Guid.TryParseExact(pair.Value[0], "D", out var id) || id == Guid.Empty) return false;
            if (pair.Key == "service") service = id; else practitioner = id;
        }
        return true;
    }
    [HttpGet("booking-links/{slug}")]
    public async Task<IActionResult> Resolve(string slug, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Selection(out var service, out var practitioner)) return BadRequest(new { code = "validation" });
        return Map(await links.ResolveAsync(slug, service, practitioner, ct));
    }
    [HttpGet("booking-context/{slug}")]
    public async Task<IActionResult> Context(string slug, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Selection(out var service, out var practitioner)) return BadRequest(new { code = "validation" });
        return Map(await links.ContextAsync(slug, service, practitioner, ct));
    }
}
