namespace Adeni.Infrastructure.Catalog;

using Adeni.Application.Catalog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class CatalogServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
            CategoryWorkflowCatalogJson.ReadFromFile(sp.GetRequiredService<IHostEnvironment>()));
        services.AddSingleton<ICategoryService, CategoryService>();
        services.AddSingleton<IBusinessCapabilitiesService, BusinessCapabilitiesService>();
        return services;
    }
}
