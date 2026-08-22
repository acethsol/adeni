namespace Adeni.Api.Controllers;

using Adeni.Application.Payments;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentsController(IPaymentProvider paymentProvider) : ControllerBase
{
    [HttpPost("initialize")]
    public async Task<IActionResult> Initialize(
        [FromBody] InitializePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await paymentProvider.InitializeAsync(request, cancellationToken);
        return ApiResults.FromResult(result, Ok);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await paymentProvider.GetAsync(id, cancellationToken);
        return ApiResults.FromResult(result, Ok);
    }
}
