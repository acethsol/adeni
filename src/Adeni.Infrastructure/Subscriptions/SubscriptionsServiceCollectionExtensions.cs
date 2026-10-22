namespace Adeni.Infrastructure.Subscriptions;

using Adeni.Application.Subscriptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
public static class SubscriptionsServiceCollectionExtensions
{
    public static IServiceCollection AddSubscriptionsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SubscriptionBillingOptions>(
            configuration.GetSection(SubscriptionBillingOptions.SectionName));

        services.PostConfigure<SubscriptionBillingOptions>(options =>
        {
            var paystackSection = configuration.GetSection("Paystack");
            if (string.IsNullOrWhiteSpace(options.Paystack.SecretKey))
            {
                options.Paystack.SecretKey = paystackSection["SecretKey"] ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(options.Paystack.WebhookSecret))
            {
                options.Paystack.WebhookSecret = paystackSection["WebhookSecret"] ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(options.Paystack.BaseUrl))
            {
                options.Paystack.BaseUrl = paystackSection["BaseUrl"] ?? options.Paystack.BaseUrl;
            }
        });

        services.AddSingleton<IPlanEntitlementsMatrix, JsonPlanEntitlementsMatrix>();
        services.AddHostedService<PlanEntitlementsMatrixStartupService>();
        services.AddScoped<IEntitlementsService, EntitlementsService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ISubscriptionBillingOrchestrator, SubscriptionBillingOrchestrator>();
        services.AddScoped<ISubscriptionMetricsService, SubscriptionMetricsService>();
        services.AddScoped<ITenantPricingResolver, TenantPricingResolver>();
        services.AddScoped<IPricingCatalogService, PricingCatalogService>();
        services.AddHostedService<PricingCatalogStartupService>();

        var provider = configuration.GetSection(SubscriptionBillingOptions.SectionName)["Provider"] ?? "Stub";

        if (string.Equals(provider, "Paystack", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<PaystackSubscriptionBillingProvider>();
            services.AddScoped<ISubscriptionBillingProvider, PaystackSubscriptionBillingProvider>();
        }
        else if (string.Equals(provider, "MarketDefault", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<PaystackSubscriptionBillingProvider>();
            services.AddScoped<PaystackSubscriptionBillingProvider>();
            services.AddScoped<StubSubscriptionBillingProvider>();
            services.AddScoped<ISubscriptionBillingProvider, MarketDefaultSubscriptionBillingProvider>();
        }
        else
        {
            services.AddScoped<ISubscriptionBillingProvider, StubSubscriptionBillingProvider>();
        }

        return services;
    }
}
