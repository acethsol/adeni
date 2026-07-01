namespace Adeni.Api.Middleware;

using System.Security.Claims;
using Adeni.Application.Auth;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class DevBusinessAuthMiddleware(RequestDelegate next)
{
    public const string DevAuth0SubHeader = "X-Dev-Auth0-Sub";

    public async Task InvokeAsync(HttpContext context, AdeniDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated != true
            && context.Request.Headers.TryGetValue(DevAuth0SubHeader, out var auth0Sub)
            && !string.IsNullOrWhiteSpace(auth0Sub))
        {
            var businessUser = await dbContext.BusinessUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Auth0Sub == auth0Sub.ToString());

            if (businessUser is not null)
            {
                var claims = new List<Claim>
                {
                    new("sub", auth0Sub.ToString()),
                    new(AdeniClaimTypes.Roles, AdeniRoles.Business),
                    new(AdeniClaimTypes.TenantId, businessUser.TenantId.ToString())
                };

                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "DevBusinessAuth"));
            }
        }

        await next(context);
    }
}
