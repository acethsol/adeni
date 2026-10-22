namespace Adeni.Infrastructure.Admin;

using Adeni.Application.Admin;
using Adeni.Application.Reporting;
using Microsoft.Extensions.DependencyInjection;

public static class AdminServiceCollectionExtensions
{
    public static IServiceCollection AddAdminModule(this IServiceCollection services)
    {
        services.AddScoped<IAdminBusinessService, AdminBusinessService>();
        services.AddScoped<IAdminCustomerService, AdminCustomerService>();
        services.AddScoped<IAdminMarketService, AdminMarketService>();
        services.AddScoped<IAdminRevenueMetricsService, AdminRevenueMetricsService>();
        services.AddScoped<IAdminReportingQueryService, AdminReportingQueryService>();
        services.AddScoped<IAdminTransactionSearchService, AdminTransactionSearchService>();
        services.AddScoped<IAdminReportExportQueryService, AdminReportExportQueryService>();
        return services;
    }
}
