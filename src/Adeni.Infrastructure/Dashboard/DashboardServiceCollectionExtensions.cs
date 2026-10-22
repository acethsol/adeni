namespace Adeni.Infrastructure.Dashboard;

using Adeni.Application.Dashboard;
using Adeni.Application.Reporting;
using Microsoft.Extensions.DependencyInjection;

public static class DashboardServiceCollectionExtensions
{
    public static IServiceCollection AddDashboardModule(this IServiceCollection services)
    {
        services.AddScoped<TenantDashboardService>();
        services.AddScoped<ITenantDashboardService>(sp => sp.GetRequiredService<TenantDashboardService>());
        services.AddScoped<ITenantReportingQueryService>(sp => sp.GetRequiredService<TenantDashboardService>());
        services.AddScoped<ITenantReportExportQueryService, TenantReportExportQueryService>();
        return services;
    }
}
