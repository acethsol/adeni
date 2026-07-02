namespace Adeni.Api.Controllers;

using Adeni.Application.Booking;
using Adeni.Domain.Common;
using Microsoft.AspNetCore.Mvc;

internal static class ApiResults
{
    public static IActionResult FromResult<T>(Result<T> result, Func<T, IActionResult> onSuccess) =>
        result.Match<IActionResult>(
            onSuccess,
            FromError);

    public static IActionResult FromResult(Result result, Func<IActionResult> onSuccess) =>
        result.Match<IActionResult>(
            onSuccess,
            FromError);

    private static IActionResult FromError(Error error) =>
        error.Code switch
        {
            "validation" => new BadRequestObjectResult(new { title = error.Message }),
            "conflict" => new ConflictObjectResult(new { title = error.Message }),
            "forbidden" => new ForbidResult(),
            _ => new NotFoundObjectResult(new { title = error.Message })
        };
}
