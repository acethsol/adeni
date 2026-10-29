namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Constants;
using Adeni.Api.Extensions;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Finance;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/finance")]
public sealed class FinanceController(
    IPaymentOrchestrator paymentOrchestrator,
    IBillingDocumentService billingDocuments,
    IStaffCompensationService staffCompensation,
    IPaymentAccessService paymentAccess,
    ITenantLocationScopeService locationScope,
    IOptions<Auth0Options> auth0Options,
    IHostEnvironment environment) : ControllerBase
{
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize(
        [FromBody] InitializePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (ResolveCustomerAuth0Sub() is null)
        {
            return Unauthorized();
        }

        if (Request.Headers.ContainsKey(IdempotencyHeaders.Key)
            && Request.GetIdempotencyKey() is null)
        {
            return this.InvalidIdempotencyKeyResult();
        }

        var result = await paymentOrchestrator.InitializeAsync(
            request,
            Request.GetIdempotencyKey(),
            cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("links")]
    [RequiresBusinessPermission(BusinessPermissions.Finance)]
    public async Task<IActionResult> CreateLink(
        [FromBody] CreatePaymentLinkRequest request,
        CancellationToken cancellationToken)
    {
        if (ResolveBusinessAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        if (request.TenantId != tenantId)
        {
            return CrossTenantDenied();
        }

        var result = await paymentOrchestrator.CreatePaymentLinkAsync(request, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var customerSub = ResolveCustomerAuth0Sub();
        var businessSub = ResolveBusinessAuth0Sub();
        if (customerSub is null && businessSub is null)
        {
            return Unauthorized();
        }

        var result = await paymentOrchestrator.GetAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return ApiResults.FromResult(result, Ok, HttpContext);
        }

        var payment = result.Value!;
        if (!await paymentAccess.CanAccessAsync(payment, customerSub, businessSub, cancellationToken))
        {
            return CrossTenantDenied();
        }

        return Ok(payment);
    }

    [HttpGet("{id:guid}/receipt")]
    public async Task<IActionResult> Receipt(Guid id, CancellationToken cancellationToken)
    {
        var customerSub = ResolveCustomerAuth0Sub();
        var businessSub = ResolveBusinessAuth0Sub();
        if (customerSub is null && businessSub is null)
        {
            return Unauthorized();
        }

        var paymentResult = await paymentOrchestrator.GetAsync(id, cancellationToken);
        if (paymentResult.IsFailure)
        {
            return ApiResults.FromResult(paymentResult, Ok, HttpContext);
        }

        var payment = paymentResult.Value!;
        if (!await paymentAccess.CanAccessAsync(payment, customerSub, businessSub, cancellationToken))
        {
            return CrossTenantDenied();
        }

        var document = await billingDocuments.GetReceiptForPaymentAsync(id, cancellationToken);
        if (document.IsSuccess)
        {
            return Ok(document.Value);
        }

        // Compat fallback to lightweight DTO if document issue fails.
        var receipt = await paymentOrchestrator.GetReceiptAsync(id, cancellationToken);
        return ApiResults.FromResult(receipt, Ok, HttpContext);
    }

    [HttpGet("compensation-export")]
    [RequiresBusinessPermission(BusinessPermissions.Finance)]
    public async Task<IActionResult> CompensationExport(
        [FromQuery] Guid tenantId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        if (ResolveBusinessAuth0Sub() is null || ResolveTenantId() is not { } callerTenantId)
        {
            return Unauthorized();
        }

        if (tenantId != callerTenantId)
        {
            return CrossTenantDenied();
        }

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            Response.Headers.Append("Deprecation", "true");
            Response.Headers.Append(
                "Link",
                "</api/v1/tenant/reporting/exports/compensation>; rel=\"successor-version\"");
            return BadRequest(new
            {
                title = "JSON compensation export is deprecated. Use GET /api/v1/tenant/reporting/exports/compensation?format=csv.",
                code = ErrorCodes.ReportFormatNotSupported,
            });
        }

        var result = await staffCompensation.ExportAsync(
            tenantId,
            from,
            to,
            CompensationExportFormat.Csv,
            cancellationToken);
        return result.Match<IActionResult>(
            payload => File(payload.Body, payload.ContentType, payload.FileName),
            error => ApiErrorResponseMapper.ToActionResult(error, HttpContext));
    }

    [HttpGet("ledger")]
    [RequiresBusinessPermission(BusinessPermissions.Finance)]
    public async Task<IActionResult> Ledger(
        [FromQuery] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        if (ResolveBusinessAuth0Sub() is null || ResolveTenantId() is not { } callerTenantId)
        {
            return Unauthorized();
        }

        if (tenantId != callerTenantId)
        {
            return CrossTenantDenied();
        }

        var scoped = await locationScope.ResolveListLocationAsync(
            tenantId,
            locationId,
            cancellationToken);
        if (scoped.IsFailure)
        {
            return ApiErrorResponseMapper.ToActionResult(scoped.Error!, HttpContext);
        }

        var result = await paymentOrchestrator.ListLedgerAsync(
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

    [HttpPost("{id:guid}/refund")]
    [RequiresBusinessPermission(BusinessPermissions.Finance)]
    public async Task<IActionResult> Refund(
        Guid id,
        [FromBody] RefundPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (ResolveBusinessAuth0Sub() is null || ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        if (request.TenantId != tenantId)
        {
            return CrossTenantDenied();
        }

        var refundRequest = request with { PaymentIntentId = id };
        var result = await paymentOrchestrator.InitiateRefundAsync(refundRequest, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
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

        var result = await paymentOrchestrator.ProcessWebhookAsync(rawBody, headers, cancellationToken);
        return result.Match<IActionResult>(
            payload => Ok(new { received = true, processed = payload.Processed, message = payload.Message }),
            error => ApiErrorResponseMapper.ToActionResult(error, HttpContext));
    }

    [HttpPost("stub/confirm")]
    public async Task<IActionResult> ConfirmStub(
        [FromBody] StubConfirmRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsDevOrTesting())
        {
            return NotFound();
        }

        var result = await paymentOrchestrator.ConfirmStubCheckoutAsync(request.Reference, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    private string? ResolveCustomerAuth0Sub()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return User.FindFirst("sub")?.Value;
        }

        if (!auth0Options.Value.Enabled
            && IsDevOrTesting()
            && Request.Headers.TryGetValue(DevCustomerAuthMiddleware.DevAuth0SubHeader, out var devSub)
            && !string.IsNullOrWhiteSpace(devSub))
        {
            return devSub.ToString();
        }

        return null;
    }

    private string? ResolveBusinessAuth0Sub()
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

    private IActionResult CrossTenantDenied() =>
        StatusCode(StatusCodes.Status403Forbidden, new { title = "Cross-tenant access denied." });
}

public sealed record StubConfirmRequest(string Reference);
