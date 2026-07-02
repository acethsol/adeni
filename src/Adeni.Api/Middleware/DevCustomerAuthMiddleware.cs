namespace Adeni.Api.Middleware;

using System.Security.Claims;
using Adeni.Application.Auth;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class DevCustomerAuthMiddleware(RequestDelegate next)
{
    public const string DevAuth0SubHeader = DevBusinessAuthMiddleware.DevAuth0SubHeader;

    public async Task InvokeAsync(HttpContext context, AdeniDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated != true
            && context.Request.Headers.TryGetValue(DevAuth0SubHeader, out var auth0Sub)
            && !string.IsNullOrWhiteSpace(auth0Sub))
        {
            var customer = await dbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Auth0Sub == auth0Sub.ToString());

            if (customer is not null)
            {
                var claims = new List<Claim>
                {
                    new("sub", auth0Sub.ToString()),
                    new(AdeniClaimTypes.Roles, AdeniRoles.Customer),
                    new(AdeniClaimTypes.PlatformUserId, customer.Id.ToString())
                };

                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "DevCustomerAuth"));
            }
        }

        await next(context);
    }
}
