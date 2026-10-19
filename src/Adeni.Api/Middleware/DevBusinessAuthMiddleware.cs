namespace Adeni.Api.Middleware;

using System.Security.Claims;
using Adeni.Application.Auth;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class DevBusinessAuthMiddleware(RequestDelegate next)
{
    public const string DevAuth0SubHeader = "X-Dev-Auth0-Sub";

    /// <summary>Query alias for SignalR WebSockets (browsers cannot send custom headers on WS).</summary>
    public const string DevAuth0SubQuery = "dev_auth0_sub";

    public async Task InvokeAsync(HttpContext context, AdeniDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated != true
            && TryResolveDevAuth0Sub(context, out var auth0Sub))
        {
            var businessUser = await dbContext.BusinessUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Auth0Sub == auth0Sub);

            if (businessUser is not null)
            {
                var claims = new List<Claim>
                {
                    new("sub", auth0Sub),
                    new(AdeniClaimTypes.Roles, AdeniRoles.Business),
                    new(AdeniClaimTypes.TenantId, businessUser.TenantId.ToString())
                };

                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "DevBusinessAuth"));
            }
        }

        await next(context);
    }

    private static bool TryResolveDevAuth0Sub(HttpContext context, out string auth0Sub)
    {
        if (context.Request.Headers.TryGetValue(DevAuth0SubHeader, out var header)
            && !string.IsNullOrWhiteSpace(header))
        {
            auth0Sub = header.ToString();
            return true;
        }

        if (context.Request.Path.StartsWithSegments("/hubs")
            && context.Request.Query.TryGetValue(DevAuth0SubQuery, out var query)
            && !string.IsNullOrWhiteSpace(query))
        {
            auth0Sub = query.ToString();
            return true;
        }

        auth0Sub = string.Empty;
        return false;
    }
}
