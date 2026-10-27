namespace Adeni.Api.Controllers;

using Adeni.Api.Common;
using Adeni.Api.Extensions;
using Adeni.Api.Auth;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Admin;
using Adeni.Application.Auth;
using Adeni.Application.Subscriptions;
using Adeni.Application.Reviews;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IAuthSyncService authSyncService,
    IStaffAccessService staffAccess,
    IOptions<Auth0Options> auth0Options) : ControllerBase
{
    [HttpPost("sync")]
    [EnableRateLimiting(RateLimitingExtensions.AuthSyncPolicy)]
    public async Task<IActionResult> Sync(
        [FromBody] SyncAuthUserRequest request,
        CancellationToken cancellationToken)
    {
        var options = auth0Options.Value;
        string? authenticatedSub = null;

        if (options.Enabled)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            authenticatedSub = User.FindFirst("sub")?.Value;
        }

        var result = await authSyncService.SyncAsync(request, authenticatedSub, cancellationToken);

        return result.Match<IActionResult>(
            profile => Ok(profile),
            error => ApiErrorResponseMapper.ToActionResult(error, HttpContext));
    }

    [HttpGet("me")]
    public IActionResult Me()
    {
        if (!auth0Options.Value.Enabled)
        {
            return StatusCode(
                StatusCodes.Status501NotImplemented,
                new { title = "Auth0 is disabled. Enable Auth0:Enabled to use this endpoint." });
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized();
        }

        var user = User.ToCurrentUser();
        return Ok(new AuthSessionResponse(
            user.UserId,
            user.Roles,
            user.TenantId?.Value,
            user.HasMfa));
    }

    /// <summary>
    /// Attach an authenticated Auth0 user (or Dev business sub) to an existing tenant via invite token.
    /// </summary>
    [HttpPost("accept-staff-invite")]
    [EnableRateLimiting(RateLimitingExtensions.AuthSyncPolicy)]
    public async Task<IActionResult> AcceptStaffInvite(
        [FromBody] AcceptStaffInviteRequest request,
        CancellationToken cancellationToken)
    {
        var options = auth0Options.Value;
        string? auth0Sub = null;
        string? email = null;

        if (options.Enabled)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Unauthorized();
            }

            auth0Sub = User.FindFirst("sub")?.Value;
            email = User.FindFirst("email")?.Value
                ?? User.FindFirst("https://adeni.io/email")?.Value;
        }
        else if (Request.Headers.TryGetValue(DevBusinessAuthMiddleware.DevAuth0SubHeader, out var devSub)
                 && !string.IsNullOrWhiteSpace(devSub))
        {
            auth0Sub = devSub.ToString();
        }

        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Unauthorized();
        }

        var result = await staffAccess.AcceptAsync(auth0Sub, email, request, cancellationToken);
        return result.Match<IActionResult>(
            payload => Ok(payload),
            error => ApiErrorResponseMapper.ToActionResult(error, HttpContext));
    }
}

[ApiController]
[Route("api/v1/admin/businesses")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminBusinessesController(
    IAdminBusinessService adminBusinessService,
    ISubscriptionBillingOrchestrator subscriptionBillingOrchestrator,
    Application.Trust.IVerificationBadgeService verificationBadges) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var stats = await adminBusinessService.GetDirectoryStatsAsync(cancellationToken);
        return Ok(stats);
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken cancellationToken = default)
    {
        var sort = ListSortQuery.ResolveOptional(
            sortBy,
            sortDir,
            AdminPendingVerificationListSort.Allowed,
            AdminPendingVerificationListSort.DefaultField,
            AdminPendingVerificationListSort.DefaultDirection);
        if (sort.IsFailure)
        {
            return ApiErrorResponseMapper.ToActionResult(sort.Error!, HttpContext);
        }

        var result = await adminBusinessService.ListPendingVerificationsAsync(
            q,
            page,
            pageSize,
            sort.Value!.SortBy,
            sort.Value.SortDirection,
            cancellationToken);

        return result.Match<IActionResult>(
            payload => Ok(payload),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => BadRequest(new { title = error.Message })
            });
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken cancellationToken = default)
    {
        var sort = ListSortQuery.ResolveOptional(
            sortBy,
            sortDir,
            AdminBusinessListSort.Allowed,
            AdminBusinessListSort.DefaultField,
            AdminBusinessListSort.DefaultDirection);
        if (sort.IsFailure)
        {
            return ApiErrorResponseMapper.ToActionResult(sort.Error!, HttpContext);
        }

        var result = await adminBusinessService.ListBusinessesAsync(
            q,
            page,
            pageSize,
            sort.Value!.SortBy,
            sort.Value.SortDirection,
            cancellationToken);

        return result.Match<IActionResult>(
            payload => Ok(payload),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => BadRequest(new { title = error.Message })
            });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await adminBusinessService.GetBusinessAsync(id, cancellationToken);
        return result.Match<IActionResult>(
            Ok,
            error => error.Code switch
            {
                "not_found" => NotFound(new { title = error.Message }),
                _ => BadRequest(new { title = error.Message }),
            });
    }

    [HttpGet("{id:guid}/commercial-terms")]
    public async Task<IActionResult> GetCommercialTerms(Guid id, CancellationToken cancellationToken)
    {
        var result = await adminBusinessService.GetTenantCommercialTermsAsync(id, cancellationToken);
        return result.Match<IActionResult>(
            Ok,
            error => NotFound(new { title = error.Message }));
    }

    [HttpGet("{id:guid}/subscription-charges")]
    public async Task<IActionResult> ListSubscriptionCharges(
        Guid id,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await subscriptionBillingOrchestrator.ListChargesForAdminAsync(
            id,
            limit,
            cancellationToken);

        return result.Match<IActionResult>(
            items => Ok(new { items }),
            error => error.Code switch
            {
                "not_found" => NotFound(new { title = error.Message }),
                _ => BadRequest(new { title = error.Message })
            });
    }

    [HttpGet("{id:guid}/commercial-audit")]
    public async Task<IActionResult> ListCommercialAudit(
        Guid id,
        [FromQuery] int limit = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await adminBusinessService.ListTenantCommercialAuditAsync(id, limit, cancellationToken);
        return result.Match<IActionResult>(
            items => Ok(new { items }),
            error => NotFound(new { title = error.Message }));
    }

    [HttpPatch("{id:guid}/commercial-terms")]
    public async Task<IActionResult> SetCommercialTerms(
        Guid id,
        [FromBody] SetTenantCommercialTermsRequest body,
        CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await adminBusinessService.SetTenantCommercialTermsAsync(
            id,
            body,
            adminId,
            cancellationToken);

        return result.Match<IActionResult>(
            _ => NoContent(),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => NotFound(new { title = error.Message })
            });
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await adminBusinessService.ApproveAsync(id, adminId, cancellationToken);

        return await result.Match<Task<IActionResult>>(
            async _ =>
            {
                await verificationBadges.GrantInitialBadgesOnApprovalAsync(id, cancellationToken);
                return NoContent();
            },
            error => Task.FromResult<IActionResult>(error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => NotFound(new { title = error.Message })
            }));
    }

    [HttpGet("{tenantId:guid}/verification-documents/{documentId:guid}/download-url")]
    public async Task<IActionResult> GetVerificationDocumentDownloadUrl(
        Guid tenantId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await adminBusinessService.GetVerificationDocumentDownloadUrlAsync(
            tenantId,
            documentId,
            adminId,
            cancellationToken);

        return result.Match<IActionResult>(
            payload => Ok(payload),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => NotFound(new { title = error.Message })
            });
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectBusinessRequest body,
        CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await adminBusinessService.RejectAsync(id, adminId, body.Reason, cancellationToken);

        return result.Match<IActionResult>(
            _ => NoContent(),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => NotFound(new { title = error.Message })
            });
    }
}

public sealed record RejectBusinessRequest(string Reason);

[ApiController]
[Route("api/v1/admin/customers")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminCustomersController(IAdminCustomerService adminCustomerService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string? email,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = string.IsNullOrWhiteSpace(q) ? email : q;
        var result = await adminCustomerService.SearchAsync(query, page, pageSize, cancellationToken);

        return result.Match<IActionResult>(
            payload => Ok(payload),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => BadRequest(new { title = error.Message })
            });
    }

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await adminCustomerService.ExportAsync(id, adminId, cancellationToken);

        return result.Match<IActionResult>(
            payload => Ok(payload),
            error => error.Code switch
            {
                _ => NotFound(new { title = error.Message })
            });
    }

    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> InitiateDelete(Guid id, CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await adminCustomerService.InitiateErasureAsync(id, adminId, cancellationToken);

        return result.Match<IActionResult>(
            _ => NoContent(),
            error => error.Code switch
            {
                "conflict" => Conflict(new { title = error.Message }),
                _ => NotFound(new { title = error.Message })
            });
    }
}

[ApiController]
[Route("api/v1/admin/reviews")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminReviewsController(IReviewService reviewService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] bool includeHidden = false,
        CancellationToken cancellationToken = default)
    {
        var result = await reviewService.ListForAdminAsync(q, page, pageSize, includeHidden, cancellationToken);

        return result.Match<IActionResult>(
            payload => Ok(payload),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => BadRequest(new { title = error.Message })
            });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Hide(Guid id, CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await reviewService.HideAsync(id, adminId, cancellationToken);

        return result.Match<IActionResult>(
            _ => NoContent(),
            error => error.Code switch
            {
                _ => NotFound(new { title = error.Message })
            });
    }
}

[ApiController]
[Route("api/v1/admin/markets")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminMarketsController(IAdminMarketService adminMarketService) : ControllerBase
{
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var stats = await adminMarketService.GetCatalogStatsAsync(cancellationToken);
        return Ok(new { totalCount = stats.TotalCount, liveCount = stats.LiveCount });
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken cancellationToken = default)
    {
        var sort = ListSortQuery.ResolveOptional(
            sortBy,
            sortDir,
            AdminMarketListSort.Allowed,
            AdminMarketListSort.DefaultField,
            AdminMarketListSort.DefaultDirection);
        if (sort.IsFailure)
        {
            return ApiErrorResponseMapper.ToActionResult(sort.Error!, HttpContext);
        }

        var result = await adminMarketService.ListAsync(
            q,
            page,
            pageSize,
            sort.Value!.SortBy,
            sort.Value.SortDirection,
            cancellationToken);

        return result.Match<IActionResult>(
            payload => Ok(payload),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => BadRequest(new { title = error.Message })
            });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateMarketRequest body,
        CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await adminMarketService.CreateAsync(body, adminId, cancellationToken);

        return result.Match<IActionResult>(
            market => CreatedAtAction(nameof(List), new { id = market.Id }, market),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                "conflict" => Conflict(new { title = error.Message }),
                _ => BadRequest(new { title = error.Message })
            });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateMarketRequest body,
        CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await adminMarketService.UpdateAsync(id, body, adminId, cancellationToken);

        return result.Match<IActionResult>(
            market => Ok(market),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                _ => NotFound(new { title = error.Message })
            });
    }

    [HttpPatch("{id}/live")]
    public async Task<IActionResult> SetLive(
        string id,
        [FromBody] SetMarketLiveRequest body,
        CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await adminMarketService.SetLiveAsync(id, body.IsLive, adminId, cancellationToken);

        return result.Match<IActionResult>(
            _ => NoContent(),
            error => error.Code switch
            {
                _ => NotFound(new { title = error.Message })
            });
    }
}

public sealed record SetMarketLiveRequest(bool IsLive);
