namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Events;
using Adeni.Application.Retention;
using Adeni.Domain.Finance.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class RetentionServiceCollectionExtensions
{
    public static IServiceCollection AddRetentionModule(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IRetentionEntitlementGuard, RetentionEntitlementGuard>();
        services.AddScoped<IPackageCatalogService, PackageCatalogService>();
        services.AddScoped<IMembershipPlanService, MembershipPlanService>();
        services.AddScoped<ICustomerPackageService, CustomerPackageService>();
        services.AddScoped<ICustomerMembershipService, CustomerMembershipService>();
        services.AddScoped<IBenefitRedemptionService, BenefitRedemptionService>();
        services.AddScoped<IPackageStorefrontService, PackageStorefrontService>();
        services.AddScoped<IPackagePurchaseActivation, PackagePurchaseActivation>();
        services.AddScoped<IDomainEventHandler<PaymentCompleted>, PackagePurchasePaymentHandler>();
        services.AddScoped<IPackageExpiryService, PackageExpiryService>();
        services.AddScoped<ICustomerPackageSummaryQuery, CustomerPackageSummaryQuery>();
        services.AddScoped<PackageExpiryHangfireJob>();
        return services;
    }
}
