namespace Adeni.Api.Controllers;

using Adeni.Application.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/categories")]
public sealed class CategoriesController(ICategoryService categories) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(
        [FromQuery] string? market,
        [FromQuery] bool wellness = true,
        [FromQuery] bool includeNonV1 = false,
        CancellationToken cancellationToken = default)
    {
        var query = new CategoryListQuery(
            MarketId: market,
            WellnessScope: wellness,
            IncludeNonV1: includeNonV1);
        var items = await categories.GetCategoriesAsync(query, cancellationToken);
        return Ok(new { items });
    }

    [HttpGet("{slug}/service-templates")]
    [AllowAnonymous]
    public IActionResult GetServiceTemplates(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return BadRequest(new { error = "Category slug is required." });
        }

        var items = categories.GetServiceTemplates(slug);
        return Ok(new { items });
    }
}
