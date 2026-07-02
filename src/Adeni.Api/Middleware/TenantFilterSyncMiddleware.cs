namespace Adeni.Api.Middleware;

using Adeni.Infrastructure.Persistence;

public sealed class TenantFilterSyncMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AdeniDbContext dbContext)
    {
        dbContext.SyncTenantFilter();
        await next(context);
    }
}
