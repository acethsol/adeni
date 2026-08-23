namespace Adeni.Infrastructure.Subscriptions;

using Adeni.Application.Subscriptions;
using Microsoft.Extensions.DependencyInjection;

public static class SubscriptionsServiceCollectionExtensions
{
    public static IServiceCollection AddSubscriptionsModule(this IServiceCollection services)
    {
        services.AddScoped<IEntitlementsService, EntitlementsService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<ISubscriptionBillingProvider, StubSubscriptionBillingProvider>();
        return services;
    }
}
