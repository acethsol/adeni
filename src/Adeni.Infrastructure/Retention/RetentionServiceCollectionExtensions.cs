namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Retention;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class RetentionServiceCollectionExtensions
{
    public static IServiceCollection AddRetentionModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IRetentionEntitlementGuard, RetentionEntitlementGuard>();
        services.AddScoped<IPackageCatalogService, PackageCatalogService>();
        services.AddScoped<ICustomerPackageService, CustomerPackageService>();
        services.AddScoped<IBenefitRedemptionService, BenefitRedemptionService>();
        services.AddScoped<IPackageExpiryService, PackageExpiryService>();
        services.AddScoped<ICustomerPackageSummaryQuery, CustomerPackageSummaryQuery>();
        services.AddScoped<PackageExpiryHangfireJob>();
        return services;
    }
}