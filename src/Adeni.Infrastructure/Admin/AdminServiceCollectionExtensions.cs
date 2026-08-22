namespace Adeni.Infrastructure.Admin;

using Adeni.Application.Admin;
using Microsoft.Extensions.DependencyInjection;

public static class AdminServiceCollectionExtensions
{
    public static IServiceCollection AddAdminModule(this IServiceCollection services)
    {
        services.AddScoped<IAdminBusinessService, AdminBusinessService>();
        services.AddScoped<IAdminCustomerService, AdminCustomerService>();
        services.AddScoped<IAdminMarketService, AdminMarketService>();
        return services;
    }
}
