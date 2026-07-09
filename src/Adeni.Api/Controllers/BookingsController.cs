namespace Adeni.Api.Controllers;

using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Booking;
using Adeni.Application.Reviews;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/bookings")]
public sealed class BookingsController(
    IBookingService bookings,
    IReviewService reviews,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await bookings.CreateAsync(auth0Sub, request, cancellationToken);
        return ApiResults.FromResult(result, payload => Created($"/api/v1/bookings/{payload.Id}", payload));
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var items = await bookings.ListForCustomerAsync(auth0Sub, cancellationToken);
        return Ok(new { items });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await bookings.CancelAsync(auth0Sub, id, cancellationToken);
        return ApiResults.FromResult(result, Ok);
    }

    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> CreateReview(
        Guid id,
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        var auth0Sub = ResolveCustomerAuth0Sub();
        if (auth0Sub is null)
        {
            return Unauthorized();
        }

        var result = await reviews.CreateForBookingAsync(auth0Sub, id, request, cancellationToken);
        return ApiResults.FromResult(result, payload => Created($"/api/v1/bookings/{id}/review", payload));
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
