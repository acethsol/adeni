namespace Adeni.Api.Controllers;

using Adeni.Application.Discovery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/discovery")]
public sealed class DiscoveryController(IDiscoveryService discovery) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await discovery.SearchAsync(lat, lng, category, page, pageSize, cancellationToken);

        return result.Match<IActionResult>(
            payload => Ok(new
            {
                items = payload.Items,
                page = payload.Page,
                pageSize = payload.PageSize,
                totalCount = payload.TotalCount
            }),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => BadRequest(new { title = error.Message })
            });
    }
}

[ApiController]
[Route("api/v1/businesses")]
public sealed class BusinessesController(IDiscoveryService discovery) : ControllerBase
{
    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        var result = await discovery.GetPublicProfileBySlugAsync(slug, cancellationToken);

        return result.Match<IActionResult>(
            profile => Ok(profile),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => NotFound(new { title = error.Message })
            });
    }
}
