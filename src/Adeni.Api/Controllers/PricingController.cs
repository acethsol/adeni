namespace Adeni.Api.Controllers;

using Adeni.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/pricing")]
public sealed class PricingController(IPricingCatalogService pricingCatalog) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetCountryPricing(
        [FromQuery] string? countryCode,
        CancellationToken cancellationToken)
    {
        var result = await pricingCatalog.GetPublicCountryPricingAsync(countryCode, cancellationToken);
        return result.Match<IActionResult>(
            Ok,
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message, code = error.Code }),
                _ => BadRequest(new { title = error.Message, code = error.Code }),
            });
    }
}
