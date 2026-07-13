namespace Adeni.Api.Controllers;

using Adeni.Application.Translation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

public sealed record TranslateRequest(
    IReadOnlyList<string>? Texts,
    string? Source,
    string? Target);

[ApiController]
[Route("api/v1/translate")]
public sealed class TranslationController(ITranslationService translation) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Translate(
        [FromBody] TranslateRequest request,
        CancellationToken cancellationToken)
    {
        var texts = request.Texts?
            .Select(text => text.Trim())
            .Where(text => text.Length > 0)
            .ToArray() ?? [];

        if (texts.Length == 0)
        {
            return Ok(new { translations = new Dictionary<string, string>() });
        }

        var source = string.IsNullOrWhiteSpace(request.Source) ? "en" : request.Source.Trim();
        var target = string.IsNullOrWhiteSpace(request.Target) ? "en" : request.Target.Trim();

        var result = await translation.TranslateAsync(texts, source, target, cancellationToken);

        return result.Match<IActionResult>(
            payload => Ok(new { translations = payload.Translations }),
            error => error.Code switch
            {
                "validation" => BadRequest(new { title = error.Message }),
                "translation_unavailable" => StatusCode(503, new { title = error.Message }),
                _ => StatusCode(502, new { title = error.Message }),
            });
    }
}
