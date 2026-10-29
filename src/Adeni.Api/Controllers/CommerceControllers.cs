namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Errors;
using Adeni.Api.Extensions;
using Adeni.Api.Middleware;
using Adeni.Application.Finance;
using Adeni.Application.Auth;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

[AttributeUsage(AttributeTargets.Class)]
public sealed class CommercePrivacyAttribute : ActionFilterAttribute
{
    public CommercePrivacyAttribute() => Order = -3000;
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store, private";
        context.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        context.HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!context.ModelState.IsValid) context.Result = new BadRequestObjectResult(new { code = "commerce.validation" });
    }
}
public abstract class CommerceControllerBase : ControllerBase
{
    protected string? Actor => User.Identity?.IsAuthenticated == true ? User.FindFirstValue("sub") : null;
    protected Guid? Tenant => Guid.TryParse(Request.Headers[TenantAccessMiddleware.TenantHeaderName], out var id) ? id : null;
    protected string? Key => Request.Headers["Idempotency-Key"].FirstOrDefault();
    protected IActionResult Map<T>(Result<T> result) => ApiResults.FromResult(result, value => Ok(value), HttpContext);
}

[ApiController, CommercePrivacy, Route("api/v1/tenant/finance"), RequiresBusinessPermission(BusinessPermissions.Finance)]
public sealed class CommerceManagementController(ICommerceService commerce) : CommerceControllerBase
{
    [HttpGet("pay-links")]
    public async Task<IActionResult> List([FromQuery] int page = 1, CancellationToken ct = default) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await commerce.ListAsync(tenant, actor, page, ct));
    [HttpPost("pay-links"), RequestSizeLimit(4096)]
    public async Task<IActionResult> Create(CreateCommerceRequest body, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await commerce.CreateAsync(tenant, actor, Key, body, ct));
    [HttpPost("pay-links/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await commerce.CancelAsync(tenant, actor, id, Key, ct));
    [HttpPost("pay-links/{id:guid}/refund")]
    public async Task<IActionResult> Refund(Guid id, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await commerce.RefundAsync(tenant, actor, id, Key, ct));
    [HttpPost("pay-links/{id:guid}/reverse-gift-redemption")]
    public async Task<IActionResult> Reverse(Guid id, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await commerce.ReverseAsync(tenant, actor, id, Key, ct));
    [HttpGet("gift-cards")]
    public async Task<IActionResult> Cards([FromQuery] int page = 1, CancellationToken ct = default) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await commerce.CardsAsync(tenant, actor, page, ct));
    [HttpGet("gift-cards/{id:guid}/transactions")]
    public async Task<IActionResult> Transactions(Guid id, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await commerce.TransactionsAsync(tenant, actor, id, ct));
}

[ApiController, CommercePrivacy, Route("api/v1/public/pay-links")]
public sealed class PublicCommerceController(ICommerceService commerce) : CommerceControllerBase
{
    // Capability is deliberately in a header, never a logged URL or a tenant override.
    private string Token => Request.Headers["X-Adeni-Pay-Token"].ToString();
    [HttpGet("resolve"), EnableRateLimiting(RateLimitingExtensions.PublicReadPolicy)]
    public async Task<IActionResult> Resolve(CancellationToken ct) => Map(await commerce.ResolveAsync(Token, ct));
    [HttpPost("checkout"), EnableRateLimiting(RateLimitingExtensions.PublicMutationPolicy), RequestSizeLimit(1024)]
    public async Task<IActionResult> Checkout(CancellationToken ct) => Map(await commerce.CheckoutAsync(Token, Actor, Key, ct));
    [HttpPost("redeem"), EnableRateLimiting(RateLimitingExtensions.PublicMutationPolicy), RequestSizeLimit(1024)]
    public async Task<IActionResult> Redeem(RedeemCommerceRequest body, CancellationToken ct) => Map(await commerce.RedeemAsync(Token, Key, body.Code, ct));
}

[ApiController, CommercePrivacy, Route("api/v1/me/gift-cards")]
public sealed class PurchasedGiftCardsController(ICommerceService commerce) : CommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Actor is not { } actor ? Unauthorized() : Map(await commerce.PurchasedCardsAsync(actor, ct));
    [HttpPost("{id:guid}/reveal"), EnableRateLimiting(RateLimitingExtensions.PublicMutationPolicy)]
    public async Task<IActionResult> Reveal(Guid id, CancellationToken ct) => Actor is not { } actor ? Unauthorized() : Map(await commerce.RevealAsync(actor, id, ct));
}

[ApiController, CommercePrivacy, Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy), Route("api/v1/admin/customers")]
public sealed class CommercePrivacyController(ICommercePrivacyService privacy) : ControllerBase
{
    [HttpGet("{id:guid}/commerce-export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct) => Ok(await privacy.ExportAsync(id, ct));
}

[ApiController, CommercePrivacy, Route("api/v1/tenant/customers"), RequiresBusinessPermission(BusinessPermissions.Bookings)]
public sealed class CustomerGiftMetadataController(Adeni.Application.Tenancy.ICustomerGiftCardsQueryService gifts) : CommerceControllerBase
{
    [HttpGet("{id:guid}/purchased-gift-cards")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] Guid? locationId, CancellationToken ct) => Actor is not { } actor || Tenant is not { } tenant ? Unauthorized() : Map(await gifts.GetAsync(tenant,id,actor,locationId,ct));
}
