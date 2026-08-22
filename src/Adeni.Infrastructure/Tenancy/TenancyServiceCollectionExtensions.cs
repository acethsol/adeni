namespace Adeni.Infrastructure.Tenancy;

using Adeni.Application.Tenancy;
using Microsoft.Extensions.DependencyInjection;

public static class TenancyServiceCollectionExtensions
{
    public static IServiceCollection AddTenancyModule(this IServiceCollection services)
    {
        services.AddScoped<IBusinessOnboardingService, BusinessOnboardingService>();
        services.AddScoped<IBusinessLocationService, BusinessLocationService>();
        return services;
    }
}
