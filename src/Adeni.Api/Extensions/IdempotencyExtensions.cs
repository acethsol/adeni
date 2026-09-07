namespace Adeni.Api.Extensions;

using Adeni.Api.Constants;
using Microsoft.AspNetCore.Mvc;

public static class IdempotencyExtensions
{
    public static string? GetIdempotencyKey(this HttpRequest request)
    {
        if (!request.Headers.TryGetValue(IdempotencyHeaders.Key, out var values))
        {
            return null;
        }

        var key = values.ToString().Trim();
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        return key.Length <= IdempotencyHeaders.MaxLength ? key : null;
    }

    public static IActionResult InvalidIdempotencyKeyResult(this ControllerBase controller) =>
        controller.BadRequest(new
        {
            title = $"Idempotency-Key must be between 1 and {IdempotencyHeaders.MaxLength} characters.",
            code = "validation.idempotency_key_invalid",
        });
}
