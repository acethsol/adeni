namespace Adeni.Api.Middleware;

using Adeni.Application.Abstractions;
using Adeni.Domain.Auditing;
using Adeni.Domain.Tenancy;

public sealed class TenantAccessMiddleware(
    RequestDelegate next,
    ILogger<TenantAccessMiddleware> logger)
{
    public const string TenantHeaderName = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context, ICurrentUser currentUser, IAuditLogWriter auditLogWriter)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/tenant", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(TenantHeaderName, out var headerValue)
            || !Guid.TryParse(headerValue, out var requestedTenantGuid))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { title = "Missing or invalid X-Tenant-Id header." });
            return;
        }

        var requestedTenant = TenantId.Create(requestedTenantGuid);
        if (requestedTenant.IsFailure)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { title = requestedTenant.Error.Message });
            return;
        }

        var userTenant = currentUser.TenantId;
        if (userTenant is null || userTenant.Value.IsEmpty || userTenant.Value.Value != requestedTenantGuid)
        {
            await LogCrossTenantAttemptAsync(context, currentUser, auditLogWriter, requestedTenantGuid);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { title = "Cross-tenant access denied." });
            return;
        }

        var tenantContext = context.RequestServices.GetRequiredService<ITenantContext>();
        tenantContext.EnableTenantFilter(requestedTenant.Value);
        await next(context);
    }

    private async Task LogCrossTenantAttemptAsync(
        HttpContext context,
        ICurrentUser currentUser,
        IAuditLogWriter auditLogWriter,
        Guid attemptedTenantId)
    {
        var correlationId = context.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? context.TraceIdentifier;

        logger.LogWarning(
            "Cross-tenant access denied for user {UserId} attempted tenant {AttemptedTenantId} on {Route}",
            currentUser.UserId ?? "anonymous",
            attemptedTenantId,
            context.Request.Path);

        await auditLogWriter.WriteAsync(new AuditEntry(
            Guid.NewGuid(),
            currentUser.UserId ?? "anonymous",
            AuditActions.CrossTenantDenied,
            "tenant",
            attemptedTenantId.ToString(),
            correlationId,
            DateTimeOffset.UtcNow,
            $"{{\"route\":\"{context.Request.Path}\"}}"));
    }
}
