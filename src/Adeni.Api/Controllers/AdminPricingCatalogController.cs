namespace Adeni.Api.Controllers;

using Adeni.Application.Subscriptions;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/admin/pricing-catalog")]
[Authorize(Policy = AuthServiceCollectionExtensions.AdminMfaPolicy)]
public sealed class AdminPricingCatalogController(IPricingCatalogService pricingCatalog) : ControllerBase
{
    [HttpGet("versions")]
    public async Task<IActionResult> ListVersions(CancellationToken cancellationToken)
    {
        var result = await pricingCatalog.ListVersionsAsync(cancellationToken);
        return result.Match<IActionResult>(
            versions => Ok(new { items = versions }),
            error => BadRequest(new { title = error.Message, code = error.Code }));
    }

    [HttpGet("versions/{code}")]
    public async Task<IActionResult> GetVersion(string code, CancellationToken cancellationToken)
    {
        var result = await pricingCatalog.GetVersionAsync(code, cancellationToken);
        return result.Match<IActionResult>(
            Ok,
            error => error.Code switch
            {
                "not_found" => NotFound(new { title = error.Message, code = error.Code }),
                _ => BadRequest(new { title = error.Message, code = error.Code }),
            });
    }

    [HttpPost("versions")]
    public async Task<IActionResult> CreateVersion(
        [FromBody] CreatePricingCatalogVersionRequest? body,
        CancellationToken cancellationToken)
    {
        if (body is null)
        {
            return BadRequest(new { title = "Request body is required.", code = "validation" });
        }

        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await pricingCatalog.CreateDraftVersionAsync(body, adminId, cancellationToken);
        return result.Match<IActionResult>(
            detail => CreatedAtAction(nameof(GetVersion), new { code = detail.Code }, detail),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message, code = error.Code }),
                "conflict" => Conflict(new { title = error.Message, code = error.Code }),
                _ => BadRequest(new { title = error.Message, code = error.Code }),
            });
    }

    [HttpPatch("versions/{code}/entries")]
    public async Task<IActionResult> UpsertEntries(
        string code,
        [FromBody] UpsertPricingCatalogEntriesRequest body,
        CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await pricingCatalog.UpsertEntriesAsync(code, body, adminId, cancellationToken);
        return result.Match<IActionResult>(
            Ok,
            error => error.Code switch
            {
                "not_found" => NotFound(new { title = error.Message, code = error.Code }),
                "validation" => BadRequest(new { title = error.Message, code = error.Code }),
                "conflict" => Conflict(new { title = error.Message, code = error.Code }),
                _ => BadRequest(new { title = error.Message, code = error.Code }),
            });
    }

    [HttpPost("versions/{code}/activate")]
    public async Task<IActionResult> ActivateVersion(string code, CancellationToken cancellationToken)
    {
        var adminId = User.FindFirst("sub")?.Value ?? "admin";
        var result = await pricingCatalog.ActivateVersionAsync(code, adminId, cancellationToken);
        return result.Match<IActionResult>(
            Ok,
            error => error.Code switch
            {
                "not_found" => NotFound(new { title = error.Message, code = error.Code }),
                "validation" => BadRequest(new { title = error.Message, code = error.Code }),
                "conflict" => Conflict(new { title = error.Message, code = error.Code }),
                _ => BadRequest(new { title = error.Message, code = error.Code }),
            });
    }
}
