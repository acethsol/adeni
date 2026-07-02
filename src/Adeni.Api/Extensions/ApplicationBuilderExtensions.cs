namespace Adeni.Api.Extensions;

using Adeni.Api.Middleware;

public static class ApplicationBuilderExtensions
{
    public static WebApplication UseAdeniSecurityPipeline(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseAuthentication();

        if (app.Environment.IsDevelopment() || app.Environment.EnvironmentName == "Testing")
        {
            app.UseMiddleware<DevBusinessAuthMiddleware>();
            app.UseMiddleware<DevCustomerAuthMiddleware>();
        }

        app.UseMiddleware<TenantScopeMiddleware>();
        app.UseMiddleware<TenantAccessMiddleware>();
        app.UseMiddleware<AuditMiddleware>();
        return app;
    }
}
