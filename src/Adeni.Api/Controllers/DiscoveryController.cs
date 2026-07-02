namespace Adeni.Api.Controllers;

using Adeni.Application.Booking;
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
        [FromQuery] string? market,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await discovery.SearchAsync(lat, lng, category, market, page, pageSize, cancellationToken);

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
public sealed class BusinessesController(
    IDiscoveryService discovery,
    IServiceCatalogService services,
    IAvailabilityService availability) : ControllerBase
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

    [HttpGet("{slug}/services")]
    [AllowAnonymous]
    public async Task<IActionResult> GetServices(string slug, CancellationToken cancellationToken)
    {
        var items = await services.ListPublicBySlugAsync(slug, cancellationToken);
        return Ok(new { items });
    }

    [HttpGet("{slug}/slots")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSlots(
        string slug,
        [FromQuery] Guid serviceId,
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var result = await availability.GetAvailableSlotsBySlugAsync(
            slug,
            serviceId,
            from,
            to,
            cancellationToken);

        return ApiResults.FromResult(result, slots => Ok(new { items = slots }));
    }
}
