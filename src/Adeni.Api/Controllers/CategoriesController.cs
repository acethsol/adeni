namespace Adeni.Api.Controllers;

using Adeni.Application.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/categories")]
public sealed class CategoriesController(ICategoryService categories) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var items = await categories.GetCategoriesAsync(cancellationToken);
        return Ok(new { items });
    }
}
