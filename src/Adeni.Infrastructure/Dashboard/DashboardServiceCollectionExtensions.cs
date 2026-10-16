namespace Adeni.Infrastructure.Dashboard;

using Adeni.Application.Dashboard;
using Microsoft.Extensions.DependencyInjection;

public static class DashboardServiceCollectionExtensions
{
    public static IServiceCollection AddDashboardModule(this IServiceCollection services)
    {
        services.AddScoped<ITenantDashboardService, TenantDashboardService>();
        return services;
    }
}
