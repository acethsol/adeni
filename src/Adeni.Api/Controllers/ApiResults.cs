namespace Adeni.Api.Controllers;

using Adeni.Api.Errors;
using Adeni.Domain.Common;
using Microsoft.AspNetCore.Mvc;

internal static class ApiResults
{
    public static IActionResult FromResult<T>(
        Result<T> result,
        Func<T, IActionResult> onSuccess,
        HttpContext httpContext) =>
        result.Match<IActionResult>(
            onSuccess,
            error => ApiErrorResponseMapper.ToActionResult(error, httpContext));

    public static IActionResult FromResult(
        Result result,
        Func<IActionResult> onSuccess,
        HttpContext httpContext) =>
        result.Match<IActionResult>(
            onSuccess,
            error => ApiErrorResponseMapper.ToActionResult(error, httpContext));
}
