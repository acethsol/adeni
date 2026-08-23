namespace Adeni.Api.Errors;

using Adeni.Api.Middleware;
using Adeni.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public sealed class GlobalExceptionHandler(
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
            ? value?.ToString()
            : null;

        logger.LogError(
            exception,
            "Unhandled exception. CorrelationId={CorrelationId}",
            correlationId);

        var problem = ApiErrorResponseMapper.ToProblemDetails(
            new Error(
                ErrorCodes.InternalServerError,
                environment.IsDevelopment()
                    ? exception.Message
                    : "An unexpected error occurred."),
            StatusCodes.Status500InternalServerError,
            httpContext);

        if (!environment.IsDevelopment())
        {
            problem.Detail = null;
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
