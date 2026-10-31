namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Errors;
using Adeni.Api.Extensions;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Reporting;
using Adeni.Domain.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/reporting/documents")]
[RequiresBusinessPermission(BusinessPermissions.Finance, BusinessPermissions.Bookings)]
public sealed class DocumentPreviewController(IDocumentPreviewService previews, IOptions<Auth0Options> auth0) : ControllerBase
{
    [HttpGet("{id:guid}/preview")]
    [EnableRateLimiting(RateLimitingExtensions.PublicReadPolicy)]
    [RequiresOnPremisesPresence]
    public async Task<IActionResult> Preview(Guid id, [FromQuery] Guid? locationId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        var sub = User.Identity?.IsAuthenticated == true ? User.FindFirstValue("sub") : null;
        if (sub is null && !auth0.Value.Enabled) sub = Request.Headers[DevBusinessAuthMiddleware.DevAuth0SubHeader].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(Request.Headers[TenantAccessMiddleware.TenantHeaderName], out var tenant)) return Unauthorized();
        return ApiResults.FromResult(await previews.GetAsync(tenant, id, sub, locationId, ct),
            pdf => File(pdf.Bytes, "application/pdf", pdf.FileName), HttpContext);
    }
}
