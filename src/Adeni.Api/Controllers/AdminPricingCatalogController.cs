namespace Adeni.Api.Controllers;

using Adeni.Application.Subscriptions;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/admin/pricing-catalog")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminPricingCatalogController(IPricingCatalogService pricingCatalog) : ControllerBase
{
    [HttpGet("versions")]
    public async Task<IActionResult> ListVersions(CancellationToken cancellationToken)
    {
        var result = await pricingCatalog.ListVersionsAsync(cancellationToken);
        return result.Match<IActionResult>(
            versions => Ok(new { items = versions }),
            error => BadRequest(new { title = error.Message, code = error.Code }));
    }

    [HttpGet("versions/{code}")]
    public async Task<IActionResult> GetVersion(string code, CancellationToken cancellationToken)
    {
        var result = await pricingCatalog.GetVersionAsync(code, cancellationToken);
        return result.Match<IActionResult>(
            Ok,
            error => error.Code switch
            {
                "not_found" => NotFound(new { title = error.Message, code = error.Code }),
                _ => BadRequest(new { title = error.Message, code = error.Code }),
            });
    }
}
