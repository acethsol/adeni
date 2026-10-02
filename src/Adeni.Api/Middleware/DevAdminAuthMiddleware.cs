namespace Adeni.Api.Middleware;

using System.Security.Claims;
using Adeni.Application.Auth;
using Adeni.Infrastructure.Persistence;

public sealed class DevAdminAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true
            && context.Request.Headers.TryGetValue(DevBusinessAuthMiddleware.DevAuth0SubHeader, out var auth0Sub)
            && auth0Sub.ToString() == DevelopmentDataSeeder.DevAdminAuth0Sub)
        {
            var claims = new List<Claim>
            {
                new("sub", auth0Sub.ToString()),
                new(AdeniClaimTypes.Roles, AdeniRoles.Admin),
            };

            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "DevAdminAuth"));
        }

        await next(context);
    }
}
