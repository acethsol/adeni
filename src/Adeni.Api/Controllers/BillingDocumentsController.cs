namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Errors;
using Adeni.Api.Extensions;
using Microsoft.AspNetCore.RateLimiting;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Finance;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/finance/documents")]
public sealed class BillingDocumentsController(
    IBillingDocumentService documents,
    IOptions<Auth0Options> auth0Options,
    Adeni.Application.Reporting.IDocumentPreviewService previews) : ControllerBase
{
    [HttpGet]
    [RequiresBusinessPermission(BusinessPermissions.Finance, BusinessPermissions.Bookings)]
    public async Task<IActionResult> List(
        [FromQuery] string? kind = null,
        [FromQuery] Guid? bookingId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (ResolveBusinessAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await documents.ListForTenantAsync(
            tenantId,
            kind,
            bookingId,
            page,
            pageSize,
            cancellationToken);
        return ApiResults.FromResult(result, items => Ok(new { items }), HttpContext);
    }

    [HttpGet("{id:guid}")]
    [RequiresBusinessPermission(BusinessPermissions.Finance, BusinessPermissions.Bookings)]
    [RequiresOnPremisesPresence]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        if (ResolveBusinessAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await documents.GetAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return ApiResults.FromResult(result, Ok, HttpContext);
        }

        if (result.Value!.TenantId != tenantId)
        {
            return ApiErrorResponseMapper.ToActionResult(
                Domain.Common.ErrorCodes.PermissionDeniedError(),
                HttpContext);
        }

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/pdf")]
    [EnableRateLimiting(RateLimitingExtensions.PublicReadPolicy)]
    [RequiresBusinessPermission(BusinessPermissions.Finance, BusinessPermissions.Bookings)]
    [RequiresOnPremisesPresence]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken, [FromQuery] Guid? locationId = null)
    {
        if (ResolveBusinessAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var doc = await documents.GetAsync(id, cancellationToken);
        if (doc.IsFailure)
        {
            return ApiResults.FromResult(doc, Ok, HttpContext);
        }

        if (doc.Value!.TenantId != tenantId)
        {
            return ApiErrorResponseMapper.ToActionResult(
                Domain.Common.ErrorCodes.PermissionDeniedError(),
                HttpContext);
        }

        if (doc.Value.BookingId is not null)
        {
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            var scoped = await previews.GetAsync(tenantId, id, ResolveBusinessAuth0Sub()!, locationId, cancellationToken);
            return ApiResults.FromResult(scoped, value => File(value.Bytes, "application/pdf", value.FileName), HttpContext);
        }

        var pdf = await documents.GetPdfAsync(id, cancellationToken);
        if (pdf.IsFailure)
        {
            return ApiResults.FromResult(pdf, _ => Ok(), HttpContext);
        }

        var fileName = $"{doc.Value.DocumentNumber}.pdf";
        return File(pdf.Value!, "application/pdf", fileName);
    }

    [HttpPost("invoices")]
    [RequiresBusinessPermission(BusinessPermissions.Finance)]
    public async Task<IActionResult> CreateInvoice(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (ResolveBusinessAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        if (request.TenantId != tenantId)
        {
            return ApiErrorResponseMapper.ToActionResult(
                Domain.Common.ErrorCodes.PermissionDeniedError(),
                HttpContext);
        }

        var result = await documents.CreateInvoiceAsync(request, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("{id:guid}/void")]
    [RequiresBusinessPermission(BusinessPermissions.Finance)]
    public async Task<IActionResult> Void(
        Guid id,
        [FromBody] VoidBillingDocumentRequest? body,
        CancellationToken cancellationToken)
    {
        if (ResolveBusinessAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await documents.VoidAsync(
            tenantId,
            id,
            body ?? new VoidBillingDocumentRequest(),
            cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("{id:guid}/email")]
    [RequiresBusinessPermission(BusinessPermissions.Finance, BusinessPermissions.Bookings)]
    public async Task<IActionResult> Email(
        Guid id,
        [FromBody] EmailBillingDocumentRequest? body,
        CancellationToken cancellationToken)
    {
        if (ResolveBusinessAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await documents.EmailAsync(
            tenantId,
            id,
            body ?? new EmailBillingDocumentRequest(),
            cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    private string? ResolveBusinessAuth0Sub()
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
