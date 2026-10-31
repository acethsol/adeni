namespace Adeni.Api.Middleware;

using Adeni.Application.Auth;
using Adeni.Application.Identity;
using Adeni.Infrastructure.Auth;
using Microsoft.Extensions.Options;

public sealed class CustomerTrustSyncMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ICustomerTrustSyncService trustSync,
        IOptions<Auth0Options> auth0Options)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (ShouldSync(path))
        {
            var sub = ResolveCustomerAuth0Sub(context, auth0Options.Value.Enabled);
            if (sub is not null)
            {
                await trustSync.SyncAsync(sub, context.User, context.RequestAborted);
            }
        }

        await next(context);
    }

    private static bool ShouldSync(string path) =>
        path.StartsWith("/api/v1/bookings", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/v1/me/", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/retention/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/v1/finance/", StringComparison.OrdinalIgnoreCase);

    private static string? ResolveCustomerAuth0Sub(HttpContext context, bool auth0Enabled)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            return context.User.FindFirst("sub")?.Value;
        }

        if (!auth0Enabled
            && context.Request.Headers.TryGetValue(DevCustomerAuthMiddleware.DevAuth0SubHeader, out var devSub)
            && !string.IsNullOrWhiteSpace(devSub))
        {
            return devSub.ToString();
        }

        return null;
    }
}
