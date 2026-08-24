namespace Adeni.Api.Controllers;

using Adeni.Api.Errors;
using Adeni.Application.Payments;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentsController(IPaymentOrchestrator paymentOrchestrator) : ControllerBase
{
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize(
        [FromBody] InitializePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await paymentOrchestrator.InitializeAsync(request, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpPost("links")]
    public async Task<IActionResult> CreateLink(
        [FromBody] CreatePaymentLinkRequest request,
        CancellationToken cancellationToken)
    {
        var result = await paymentOrchestrator.CreatePaymentLinkAsync(request, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await paymentOrchestrator.GetAsync(id, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("ledger")]
    public async Task<IActionResult> Ledger(
        [FromQuery] Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
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
        var result = await paymentOrchestrator.ConfirmStubCheckoutAsync(request.Reference, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }
}

public sealed record StubConfirmRequest(string Reference);
