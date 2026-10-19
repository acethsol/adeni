namespace Adeni.Api.Controllers;

using Adeni.Api.Extensions;
using Adeni.Application.Finance;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[Route("api/v1/public/documents")]
public sealed class PublicBillingDocumentsController(IBillingDocumentService documents) : ControllerBase
{
    [HttpGet("{token}")]
    [EnableRateLimiting(RateLimitingExtensions.PublicReadPolicy)]
    public async Task<IActionResult> GetByToken(string token, CancellationToken cancellationToken)
    {
        var result = await documents.GetByPublicTokenAsync(token, cancellationToken);
        return ApiResults.FromResult(result, Ok, HttpContext);
    }

    [HttpGet("{token}/pdf")]
    [EnableRateLimiting(RateLimitingExtensions.PublicReadPolicy)]
    public async Task<IActionResult> PdfByToken(string token, CancellationToken cancellationToken)
    {
        var doc = await documents.GetByPublicTokenAsync(token, cancellationToken);
        if (doc.IsFailure)
        {
            return ApiResults.FromResult(doc, Ok, HttpContext);
        }

        var pdf = await documents.GetPdfAsync(doc.Value!.Id, cancellationToken);
        if (pdf.IsFailure)
        {
            return ApiResults.FromResult(pdf, _ => Ok(), HttpContext);
        }

        return File(pdf.Value!, "application/pdf", $"{doc.Value.DocumentNumber}.pdf");
    }
}
