namespace Adeni.Api.Controllers.Retention;

using Adeni.Api.Constants;
using Adeni.Api.Extensions;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Retention;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

/// <summary>
/// Discover storefront for prepaid packages (Sprint 37.1): anonymous public catalog, customer purchase
/// (Finance payment intent; credits granted only on <c>PaymentCompleted</c>) and the customer's own redeemable credits.
/// </summary>
[ApiController]
[Route("api/v1/businesses/{slug}/retention")]
public sealed class PublicRetentionController(
    IPackageStorefrontService storefront,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet("packages")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingExtensions.PublicReadPolicy)]
    public async Task<IActionResult> Catalog(string slug, CancellationToken cancellationToken) =>
        ApiResults.FromResult(await storefront.GetPublicCatalogAsync(slug, cancellationToken), Ok, HttpContext);

    [HttpPost("packages/{id:guid}/purchase")]
    [EnableRateLimiting(RateLimitingExtensions.PublicMutationPolicy)]
    public async Task<IActionResult> Purchase(string slug, Guid id, CancellationToken cancellationToken)
    {
        var sub = ResolveCustomerAuth0Sub();
        if (sub is null)
        {
            return Unauthorized();
        }

        if (Request.Headers.ContainsKey(IdempotencyHeaders.Key) && Request.GetIdempotencyKey() is null)
        {
            return this.InvalidIdempotencyKeyResult();
        }

        return ApiResults.FromResult(
            await storefront.PurchaseAsync(sub, slug, id, Request.GetIdempotencyKey(), cancellationToken),
            Ok,
            HttpContext);
    }

    [HttpGet("redeemable-benefits")]
    public async Task<IActionResult> RedeemableBenefits(
        string slug,
        [FromQuery] Guid? businessLocationId,
        [FromQuery] Guid[]? serviceOfferingIds,
        CancellationToken cancellationToken)
    {
        var sub = ResolveCustomerAuth0Sub();
        if (sub is null)
        {
            return Unauthorized();
        }

        return ApiResults.FromResult(
            await storefront.ListRedeemableForCustomerAsync(sub, slug, businessLocationId, serviceOfferingIds, cancellationToken),
            Ok,
            HttpContext);
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
