namespace Adeni.Api.Middleware;

using Microsoft.AspNetCore.Http;

public static class AdminAuditRules
{
    public static bool IsAuditableAdminRequest(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/admin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (IsSensitiveAdminRead(context))
        {
            return true;
        }

        return HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method);
    }

    public static bool IsAuditableAdminMutation(HttpContext context) =>
        IsAuditableAdminRequest(context) && !IsSensitiveAdminRead(context);

    public static bool IsSuccessfulResponse(HttpContext context) =>
        context.Response.StatusCode is >= 200 and < 300;

    /// <summary>
    /// Service-layer audits carry richer entity metadata; skip generic middleware rows for those routes.
    /// </summary>
    public static bool IsMiddlewareAuditSuppressed(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.Contains("/approve", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/reject", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.Contains("/commercial-terms", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsPatch(context.Request.Method))
        {
            return true;
        }

        if (path.Contains("/admin/markets", StringComparison.OrdinalIgnoreCase)
            && (HttpMethods.IsPost(context.Request.Method)
                || HttpMethods.IsPut(context.Request.Method)
                || HttpMethods.IsPatch(context.Request.Method)))
        {
            return true;
        }

        if (path.Contains("/admin/pricing-catalog", StringComparison.OrdinalIgnoreCase)
            && (HttpMethods.IsPost(context.Request.Method)
                || HttpMethods.IsPut(context.Request.Method)
                || HttpMethods.IsPatch(context.Request.Method)))
        {
            return true;
        }

        if (path.Contains("/admin/customers/", StringComparison.OrdinalIgnoreCase)
            && path.EndsWith("/delete", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsPost(context.Request.Method))
        {
            return true;
        }

        if (path.Contains("/admin/reviews/", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsDelete(context.Request.Method))
        {
            return true;
        }

        if (path.Contains("/admin/verification/", StringComparison.OrdinalIgnoreCase)
            && (path.EndsWith("/grant", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/revoke", StringComparison.OrdinalIgnoreCase))
            && HttpMethods.IsPost(context.Request.Method))
        {
            return true;
        }

        return false;
    }

    public static string MapAction(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.Contains("/approve", StringComparison.OrdinalIgnoreCase))
        {
            return Domain.Auditing.AuditActions.BusinessApproved;
        }

        if (path.Contains("/reject", StringComparison.OrdinalIgnoreCase))
        {
            return Domain.Auditing.AuditActions.BusinessRejected;
        }

        if (path.Contains("/suspend", StringComparison.OrdinalIgnoreCase))
        {
            return Domain.Auditing.AuditActions.BusinessSuspended;
        }

        if (path.Contains("/reviews/", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsDelete(context.Request.Method))
        {
            return Domain.Auditing.AuditActions.ReviewHidden;
        }

        if (path.Contains("/admin/reporting", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsGet(context.Request.Method))
        {
            return Domain.Auditing.AuditActions.AdminReportingRead;
        }

        if (path.Contains("/admin/metrics", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsGet(context.Request.Method))
        {
            return Domain.Auditing.AuditActions.AdminMetricsRead;
        }

        if (path.Contains("/admin/transactions", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsGet(context.Request.Method))
        {
            return Domain.Auditing.AuditActions.AdminTransactionsRead;
        }

        if (context.Request.Path.StartsWithSegments("/api/v1/admin/audit-logs", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsGet(context.Request.Method))
        {
            return Domain.Auditing.AuditActions.AdminAuditLogsRead;
        }

        if (path.Contains("/export", StringComparison.OrdinalIgnoreCase))
        {
            return Domain.Auditing.AuditActions.CustomerExported;
        }

        if (path.EndsWith("/delete", StringComparison.OrdinalIgnoreCase))
        {
            return Domain.Auditing.AuditActions.CustomerDeleted;
        }

        if (path.Contains("/admin/markets", StringComparison.OrdinalIgnoreCase))
        {
            if (HttpMethods.IsPost(context.Request.Method))
            {
                return Domain.Auditing.AuditActions.MarketCreated;
            }

            if (path.EndsWith("/live", StringComparison.OrdinalIgnoreCase)
                && HttpMethods.IsPatch(context.Request.Method))
            {
                return Domain.Auditing.AuditActions.MarketLiveToggled;
            }

            if (HttpMethods.IsPut(context.Request.Method))
            {
                return Domain.Auditing.AuditActions.MarketUpdated;
            }
        }

        return $"admin.{context.Request.Method.ToLowerInvariant()}";
    }

    public static string? ExtractEntityId(HttpContext context)
    {
        var segments = context.Request.Path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (Guid.TryParse(segments[i + 1], out _))
            {
                return segments[i + 1];
            }
        }

        return null;
    }

    private static bool IsSensitiveAdminRead(HttpContext context) =>
        HttpMethods.IsGet(context.Request.Method)
        && (context.Request.Path.StartsWithSegments("/api/v1/admin/reporting", StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.StartsWithSegments("/api/v1/admin/metrics", StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.StartsWithSegments("/api/v1/admin/transactions", StringComparison.OrdinalIgnoreCase)
            || context.Request.Path.StartsWithSegments("/api/v1/admin/audit-logs", StringComparison.OrdinalIgnoreCase));
}
