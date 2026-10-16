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
            configuration.GetSection(SubscriptionBillingOptions.PaystackSectionName));
        services.AddScoped<IEntitlementsService, EntitlementsService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ISubscriptionBillingOrchestrator, SubscriptionBillingOrchestrator>();
        services.AddScoped<ISubscriptionBillingProvider, StubSubscriptionBillingProvider>();
        services.AddScoped<ISubscriptionMetricsService, SubscriptionMetricsService>();
        return services;
    }
}
