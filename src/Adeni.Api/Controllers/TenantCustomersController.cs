namespace Adeni.Api.Controllers;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Api.Common;
using Adeni.Api.Errors;
using Adeni.Api.Middleware;
using Adeni.Application.Auth;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/tenant/customers")]
[RequiresPortalPermission(PortalPermissions.Bookings, PortalPermissions.BookingsSelf)]
public sealed class TenantCustomersController(ITenantCustomerService customers) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? discover = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken cancellationToken = default)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var sort = ListSortQuery.ResolveOptional(
            sortBy,
            sortDir,
            TenantCustomerListSort.Allowed,
            TenantCustomerListSort.DefaultField,
            TenantCustomerListSort.DefaultDirection);
        if (sort.IsFailure)
        {
            return ApiResults.FromResult(
                Result.Failure<TenantCustomerListResponse>(sort.Error),
                Ok,
                HttpContext);
        }

        var result = await customers.ListForTenantAsync(
            tenantId,
            search,
            page,
            pageSize,
            discover,
            sort.Value!.SortBy,
            sort.Value.SortDirection,
            cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
    {
        if (ResolveTenantId() is not { } tenantId)
        {
            return Unauthorized();
        }

        var result = await customers.GetByIdAsync(tenantId, id, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    private Guid? ResolveTenantId()
    {
        var tenantClaim = User.FindFirstValue(AdeniClaimTypes.TenantId);
        if (Guid.TryParse(tenantClaim, out var tenantId))
        {
            return tenantId;
        }

        if (Request.Headers.TryGetValue(TenantAccessMiddleware.TenantHeaderName, out var headerValue)
            && Guid.TryParse(headerValue, out tenantId))
        {
            return tenantId;
        }

        return null;
    }
}
