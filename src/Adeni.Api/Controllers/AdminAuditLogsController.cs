namespace Adeni.Api.Controllers;

using Adeni.Api.Errors;
using Adeni.Application.Admin;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/admin/audit-logs")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminAuditLogsController(IAdminAuditLogQueryService auditLogQuery) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? action = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var result = await auditLogQuery.ListAsync(
            action,
            from,
            to,
            page,
            pageSize,
            cancellationToken);

        return result.Match<IActionResult>(
            payload => Ok(payload),
            error => ApiErrorResponseMapper.ToActionResult(error, HttpContext));
    }
}
