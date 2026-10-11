namespace Adeni.Api.Controllers;

using Adeni.Api.Extensions;
using Adeni.Application.Booking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[Route("api/v1/public/queue")]
public sealed class PublicQueueController(IQueueService queue) : ControllerBase
{
    [HttpPost("{slug}/join")]
    [EnableRateLimiting(RateLimitingExtensions.PublicMutationPolicy)]
    public async Task<IActionResult> Join(
        string slug,
        [FromBody] JoinQueueRequest request,
        CancellationToken cancellationToken)
    {
        var result = await queue.JoinBySlugAsync(slug, request, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("tickets/{token}")]
    [EnableRateLimiting(RateLimitingExtensions.PublicReadPolicy)]
    public async Task<IActionResult> GetTicket(string token, CancellationToken cancellationToken)
    {
        var result = await queue.GetByPublicTokenAsync(token, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }
}
