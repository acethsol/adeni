namespace Adeni.Api.Middleware;

using Microsoft.AspNetCore.Http;

public static class AdminAuditRules
{
    public static bool IsAuditableAdminMutation(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api/v1/admin", StringComparison.OrdinalIgnoreCase)
        && (HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method));

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
}
