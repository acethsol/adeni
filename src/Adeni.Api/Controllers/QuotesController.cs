namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Booking;
using Adeni.Application.Reporting;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/tenant/quotes")]
[RequiresBusinessPermission(BusinessPermissions.Quotes)]
public sealed class TenantQuotesController(
    IQuoteRequestService quotes,
    IReportRenderer reportRenderer,
    IReportPdfBrandingLoader pdfBrandingLoader,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var items = await quotes.ListForTenantAsync(tenantId, auth0Sub, locationId, cancellationToken);
        return Ok(new { items });
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var quoteResult = await quotes.GetForTenantByIdAsync(tenantId, auth0Sub, id, cancellationToken);
        if (quoteResult.IsFailure)
        {
            return ApiErrorResponseMapper.ToActionResult(quoteResult.Error!, HttpContext);
        }

        var quote = quoteResult.Value!;
        var branding = await pdfBrandingLoader.LoadForTenantAsync(tenantId, cancellationToken);

        var data = new QuoteDocumentReportData(
            branding.BusinessName,
            quote.Id.ToString(),
            quote.Description,
            quote.ServiceAddress,
            quote.QuotedAmount,
            quote.QuotedCurrency,
            quote.QuoteNotes,
            quote.ProposedStartAt,
            quote.ProposedEndAt,
            quote.ExpiresAt,
            quote.Status,
            branding.BusinessLogoBytes);

        var pdf = await reportRenderer.RenderDocumentAsync(ReportKind.Quote, data, cancellationToken);
        return pdf.Match<IActionResult>(
            bytes => File(bytes, "application/pdf", $"quote-{id}.pdf"),
            error => ApiErrorResponseMapper.ToActionResult(error, HttpContext));
    }

    [HttpPost("{id:guid}/offer")]
    public async Task<IActionResult> SubmitOffer(
        Guid id,
        [FromBody] SubmitQuoteOfferRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveAuth0Sub();
        if (auth0Sub is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await quotes.SubmitOfferAsync(tenantId, auth0Sub, id, request, cancellationToken);
        return ApiResults.FromResult(
            result,
            payload => Ok(payload),
            HttpContext);
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
[Route("api/v1/quotes")]
public sealed class CustomerQuotesController(
    IQuoteRequestService quotes,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var items = await quotes.ListForCustomerAsync(auth0Sub, cancellationToken);
        return Ok(new { items });
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await quotes.AcceptAsync(auth0Sub, id, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("{id:guid}/decline")]
    public async Task<IActionResult> Decline(Guid id, CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await quotes.DeclineAsync(auth0Sub, id, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
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
