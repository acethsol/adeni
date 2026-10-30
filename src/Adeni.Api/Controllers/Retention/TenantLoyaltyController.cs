namespace Adeni.Api.Controllers.Retention;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Constants;
using Adeni.Api.Extensions;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Retention;
using Adeni.Domain.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

/// <summary>
/// Loyalty program + points ledger (Sprint 37.3-A). Every route needs the <c>loyaltyBasic</c> plan entitlement
/// (enforced in the services). Program changes are owner-only; manual adjustments are owner/manager and need an
/// <c>Idempotency-Key</c>. Automatic earning is slice 37.3-B; redeeming arrives in 37.3-C.
/// </summary>
[ApiController]
[Route("api/v1/tenant/retention/loyalty")]
public sealed class TenantLoyaltyController(
    ILoyaltyProgramService programs,
    ILoyaltyAccountService accounts,
    ILoyaltyTierService tiers,
    ILoyaltyRewardCatalogService rewards,
    IReferralProgramService referralPrograms,
    IReferralCodeService referralCodes,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpGet("program")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> GetProgram(CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await programs.GetAsync(actor.TenantId, cancellationToken), Ok, HttpContext);
    }

    [HttpPut("program")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> UpsertProgram([FromBody] UpsertLoyaltyProgramRequest request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(
            await programs.UpsertAsync(actor.TenantId, actor.Actor, request, cancellationToken), Ok, HttpContext);
    }

    [HttpGet("customers/{tenantCustomerId:guid}/account")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> GetAccount(Guid tenantCustomerId, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(
            await accounts.GetForCustomerAsync(actor.TenantId, tenantCustomerId, cancellationToken), Ok, HttpContext);
    }

    [HttpGet("customers/{tenantCustomerId:guid}/transactions")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> ListTransactions(
        Guid tenantCustomerId, [FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(
            await accounts.ListTransactionsAsync(actor.TenantId, tenantCustomerId, limit, cancellationToken), Ok, HttpContext);
    }

    [HttpPost("customers/{tenantCustomerId:guid}/adjust")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> Adjust(
        Guid tenantCustomerId, [FromBody] AdjustLoyaltyPointsRequest request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        if (Request.Headers.ContainsKey(IdempotencyHeaders.Key) && Request.GetIdempotencyKey() is null)
        {
            return this.InvalidIdempotencyKeyResult();
        }

        var result = await accounts.AdjustAsync(
            actor.TenantId, actor.Actor, tenantCustomerId, request, Request.GetIdempotencyKey(), cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("tiers")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> ListTiers(CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await tiers.ListAsync(actor.TenantId, cancellationToken), Ok, HttpContext);
    }

    [HttpPut("tiers")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> ReplaceTiers([FromBody] ReplaceLoyaltyTiersRequest request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await tiers.ReplaceAsync(actor.TenantId, actor.Actor, request, cancellationToken), Ok, HttpContext);
    }

    [HttpGet("rewards")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> ListRewards([FromQuery] bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await rewards.ListAsync(actor.TenantId, activeOnly, cancellationToken), Ok, HttpContext);
    }

    [HttpPost("rewards")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> CreateReward([FromBody] CreateLoyaltyRewardRequest request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(
            await rewards.CreateAsync(actor.TenantId, actor.Actor, request, cancellationToken),
            payload => Created($"/api/v1/tenant/retention/loyalty/rewards/{payload.Id}", payload),
            HttpContext);
    }

    [HttpPatch("rewards/{rewardId:guid}")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> UpdateReward(
        Guid rewardId, [FromBody] UpdateLoyaltyRewardRequest request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(
            await rewards.UpdateAsync(actor.TenantId, actor.Actor, rewardId, request, cancellationToken), Ok, HttpContext);
    }

    [HttpGet("referrals/program")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> GetReferralProgram(CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(await referralPrograms.GetAsync(actor.TenantId, cancellationToken), Ok, HttpContext);
    }

    [HttpPut("referrals/program")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> UpsertReferralProgram(
        [FromBody] UpsertReferralProgramRequest request, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(
            await referralPrograms.UpsertAsync(actor.TenantId, actor.Actor, request, cancellationToken), Ok, HttpContext);
    }

    [HttpGet("customers/{tenantCustomerId:guid}/referral-code")]
    [RequiresBusinessPermission(BusinessPermissions.Retention, BusinessPermissions.Bookings)]
    public async Task<IActionResult> GetReferralCode(Guid tenantCustomerId, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        return ApiResults.FromResult(
            await referralCodes.GetOrCreateForCustomerAsync(actor.TenantId, tenantCustomerId, cancellationToken),
            Ok,
            HttpContext);
    }

    [HttpPost("customers/{tenantCustomerId:guid}/rewards/{rewardId:guid}/redeem")]
    [RequiresBusinessPermission(BusinessPermissions.Retention)]
    public async Task<IActionResult> RedeemReward(
        Guid tenantCustomerId, Guid rewardId, CancellationToken cancellationToken)
    {
        if (ResolveActor() is not { } actor) return Unauthorized();
        if (Request.Headers.ContainsKey(IdempotencyHeaders.Key) && Request.GetIdempotencyKey() is null)
        {
            return this.InvalidIdempotencyKeyResult();
        }

        return ApiResults.FromResult(
            await rewards.RedeemForCustomerAsync(
                actor.TenantId, actor.Actor, tenantCustomerId, rewardId, Request.GetIdempotencyKey(), cancellationToken),
            Ok,
            HttpContext);
    }

    private (Guid TenantId, RetentionActor Actor)? ResolveActor()
    {
        var access = PortalAccessHttpContext.Get(HttpContext);
        var sub = User.Identity?.IsAuthenticated == true ? User.FindFirstValue("sub") : null;
        if (sub is null && !auth0Options.Value.Enabled)
        {
            sub = Request.Headers[DevBusinessAuthMiddleware.DevAuth0SubHeader].FirstOrDefault();
        }

        // Tenant comes from the resolved business user (DB), never from client input alone;
        // TenantAccessMiddleware additionally requires X-Tenant-Id to match.
        return access is not null && !string.IsNullOrWhiteSpace(sub)
            ? (access.TenantId, new RetentionActor(sub, access.PermissionRole))
            : null;
    }
}
