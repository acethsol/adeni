namespace Adeni.Api.Controllers;

using Adeni.Api.Common;
using Adeni.Api.Errors;
using Adeni.Application.Admin;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/admin/transactions")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminTransactionsController(IAdminTransactionSearchService transactionSearch) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string? kind = "all",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken cancellationToken = default)
    {
        var sort = ListSortQuery.ResolveOptional(
            sortBy,
            sortDir,
            AdminTransactionListSort.Allowed,
            AdminTransactionListSort.DefaultField,
            AdminTransactionListSort.DefaultDirection);
        if (sort.IsFailure)
        {
            return ApiErrorResponseMapper.ToActionResult(sort.Error!, HttpContext);
        }

        var result = await transactionSearch.SearchAsync(
            q,
            kind,
            page,
            pageSize,
            sort.Value!.SortBy,
            sort.Value.SortDirection,
            cancellationToken);

        return result.Match<IActionResult>(
            payload => Ok(payload),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message, code = error.Code }),
                _ => BadRequest(new { title = error.Message, code = error.Code }),
            });
    }
}
