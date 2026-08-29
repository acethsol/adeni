namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Payments;
using Adeni.Infrastructure.Auth;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentsController(
    IPaymentOrchestrator paymentOrchestrator,
    AdeniDbContext dbContext,
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

        var result = await paymentOrchestrator.InitializeAsync(request, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("links")]
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
        if (!await CanAccessPaymentAsync(payment, customerSub, businessSub, cancellationToken))
        {
            return CrossTenantDenied();
        }

        return Ok(payment);
    }

    [HttpGet("ledger")]
    public async Task<IActionResult> Ledger(
        [FromQuery] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
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

        var result = await paymentOrchestrator.ListLedgerAsync(tenantId, page, pageSize, cancellationToken);
        return ApiResults.FromResult(
            result,
            items => Ok(new { items }),
            HttpContext);
    }

    [HttpPost("{id:guid}/refund")]
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

    private async Task<bool> CanAccessPaymentAsync(
        PaymentIntentResponse payment,
        string? customerSub,
        string? businessSub,
        CancellationToken cancellationToken)
    {
        if (businessSub is not null)
        {
            var isBusinessMember = await dbContext.BusinessUsers
                .AsNoTracking()
                .AnyAsync(
                    b => b.Auth0Sub == businessSub && b.TenantId == payment.TenantId,
                    cancellationToken);

            if (isBusinessMember)
            {
                return true;
            }
        }

        if (customerSub is null || payment.BookingId is null)
        {
            return false;
        }

        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Auth0Sub == customerSub, cancellationToken);

        if (customer is null)
        {
            return false;
        }

        return await dbContext.Bookings
            .AsNoTracking()
            .AnyAsync(
                b => b.Id == payment.BookingId && b.CustomerId == customer.Id,
                cancellationToken);
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
