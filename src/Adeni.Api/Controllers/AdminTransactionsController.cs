namespace Adeni.Api.Controllers;

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
        CancellationToken cancellationToken = default)
    {
        var result = await transactionSearch.SearchAsync(q, kind, page, pageSize, cancellationToken);

        return result.Match<IActionResult>(
            payload => Ok(payload),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message, code = error.Code }),
                _ => BadRequest(new { title = error.Message, code = error.Code }),
            });
    }
}
