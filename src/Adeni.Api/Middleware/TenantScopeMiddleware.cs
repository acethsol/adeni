namespace Adeni.Api.Middleware;

using Adeni.Application.Abstractions;

public sealed class TenantScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        tenantContext.DisableTenantFilter();
        await next(context);
    }
}
