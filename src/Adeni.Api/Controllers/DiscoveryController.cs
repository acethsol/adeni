namespace Adeni.Api.Controllers;

using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Booking;
using Adeni.Application.Discovery;
using Adeni.Application.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sort = "distance",
        [FromQuery] int? minRating = null,
        CancellationToken cancellationToken = default)
    {
        var discoverySort = string.Equals(sort, "featured", StringComparison.OrdinalIgnoreCase)
            ? DiscoverySort.Featured
            : DiscoverySort.Distance;

        var result = await discovery.SearchAsync(
            lat,
            lng,
            category,
            market,
            q,
            page,
            pageSize,
            discoverySort,
            minRating,
            cancellationToken);

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
    IAvailabilityService availability,
    IReviewService reviews,
    IQuoteRequestService quoteRequests,
    IOptions<Auth0Options> auth0Options) : ControllerBase
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

    [HttpGet("{slug}/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReviews(
        string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await reviews.ListPublicBySlugAsync(slug, page, pageSize, cancellationToken);

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
                _ => NotFound(new { title = error.Message })
            });
    }

    [HttpPost("{slug}/quote-requests")]
    public async Task<IActionResult> CreateQuoteRequest(
        string slug,
        [FromBody] CreateQuoteRequestRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await quoteRequests.CreateBySlugAsync(auth0Sub, slug, request, cancellationToken);
        return ApiResults.FromResult(
            result,
            payload => Created($"/api/v1/businesses/{slug}/quote-requests/{payload.Id}", payload));
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
