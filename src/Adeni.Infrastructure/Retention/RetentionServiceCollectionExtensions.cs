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
        services.AddScoped<ILoyaltyProgramService, LoyaltyProgramService>();
        services.AddScoped<ILoyaltyAccountService, LoyaltyAccountService>();
        services.AddScoped<ILoyaltyEarnService, LoyaltyEarnService>();
        services.AddScoped<ILoyaltyRedemptionService, LoyaltyRedemptionService>();
        services.AddScoped<ILoyaltyCustomerService, LoyaltyCustomerService>();
        services.AddScoped<ILoyaltyExpiryService, LoyaltyExpiryService>();
        services.AddScoped<LoyaltyExpiryHangfireJob>();
        services.AddScoped<ILoyaltyTierService, LoyaltyTierService>();
        services.AddScoped<ILoyaltyRewardCatalogService, LoyaltyRewardCatalogService>();
        services.AddScoped<IReferralProgramService, ReferralProgramService>();
        services.AddScoped<IReferralCodeService, ReferralCodeService>();
        services.AddScoped<IReferralQualificationService, ReferralQualificationService>();
        services.AddScoped<IBenefitRedemptionService, BenefitRedemptionService>();
        services.AddScoped<IRetentionAnalyticsService, RetentionAnalyticsService>();
        services.AddScoped<IPackageStorefrontService, PackageStorefrontService>();
        services.AddScoped<IPackagePurchaseActivation, PackagePurchaseActivation>();
        services.AddScoped<IDomainEventHandler<PaymentCompleted>, PackagePurchasePaymentHandler>();
        services.AddScoped<IPackageExpiryService, PackageExpiryService>();
        services.AddScoped<ICustomerPackageSummaryQuery, CustomerPackageSummaryQuery>();
        services.AddScoped<PackageExpiryHangfireJob>();
        services.AddScoped<IMembershipBillingCoordinator, MembershipBillingCoordinator>();
        services.AddScoped<IMembershipStorefrontService, MembershipStorefrontService>();
        services.AddScoped<IMembershipMrrService, MembershipMrrService>();
        services.AddScoped<IDomainEventHandler<PaymentCompleted>, MembershipPaymentHandler>();
        services.AddScoped<IDomainEventHandler<PaymentFailed>, MembershipPaymentHandler>();
        services.AddScoped<MembershipBillingHangfireJob>();
        return services;
    }
}
