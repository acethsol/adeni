namespace Adeni.Api.Errors;

using Adeni.Api.Middleware;
using Adeni.Domain.Common;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Maps domain <see cref="Error"/> values to RFC 7807 JSON bodies returned by the API.
/// </summary>
internal static class ApiErrorResponseMapper
{
    public static ObjectResult ToActionResult(Error error, HttpContext httpContext)
    {
        var statusCode = ResolveStatusCode(error.Code);
        var problem = ToProblemDetails(error, statusCode, httpContext);
        return new ObjectResult(problem) { StatusCode = statusCode };
    }

    public static ProblemDetails ToProblemDetails(
        Error error,
        int statusCode,
        HttpContext? httpContext = null)
    {
        var correlationId = ResolveCorrelationId(httpContext);
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = error.Message,
            Status = statusCode,
            Detail = error.Message,
        };

        problem.Extensions["code"] = error.Code;

        if (error.Params is { Count: > 0 })
        {
            problem.Extensions["params"] = error.Params;
        }

        if (correlationId is not null)
        {
            problem.Extensions["correlationId"] = correlationId;
        }

        return problem;
    }

    public static int ResolveStatusCode(string code) =>
        code switch
        {
            "validation" => StatusCodes.Status400BadRequest,
            "not_found" => StatusCodes.Status404NotFound,
            "forbidden" => StatusCodes.Status403Forbidden,
            "conflict" => StatusCodes.Status409Conflict,
            ErrorCodes.InternalServerError => StatusCodes.Status500InternalServerError,
            _ when code.StartsWith("validation.", StringComparison.Ordinal) =>
                StatusCodes.Status400BadRequest,
            _ when code.StartsWith("not_found.", StringComparison.Ordinal) =>
                StatusCodes.Status404NotFound,
            _ when code.StartsWith("conflict.", StringComparison.Ordinal) =>
                StatusCodes.Status409Conflict,
            _ when code.StartsWith("subscription.", StringComparison.Ordinal) =>
                StatusCodes.Status403Forbidden,
            _ when code.StartsWith("auth.", StringComparison.Ordinal) =>
                StatusCodes.Status403Forbidden,
            _ when code.StartsWith("forbidden.", StringComparison.Ordinal) =>
                StatusCodes.Status403Forbidden,
            _ when code.StartsWith("entitlement.", StringComparison.Ordinal) =>
                StatusCodes.Status403Forbidden,
            _ when code.StartsWith("internal.", StringComparison.Ordinal) =>
                StatusCodes.Status500InternalServerError,
            _ when code.StartsWith("booking.", StringComparison.Ordinal) => ResolveBookingStatusCode(code),
            _ => StatusCodes.Status404NotFound,
        };

    private static int ResolveBookingStatusCode(string code) =>
        code switch
        {
            ErrorCodes.SlotExpired => StatusCodes.Status400BadRequest,
            ErrorCodes.SlotUnavailable or ErrorCodes.SlotLocked =>
                StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

    private static string? ResolveCorrelationId(HttpContext? httpContext)
    {
        if (httpContext?.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value) == true
            && value is string correlationId
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        return null;
    }
}
