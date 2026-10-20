namespace Adeni.Infrastructure.Tests.Subscriptions;

using Adeni.Application.Abstractions;
using Adeni.Application.Subscriptions;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Subscriptions;
using Microsoft.Extensions.DependencyInjection;

internal static class EntitlementsTestServiceRegistration
{
    internal static IServiceCollection AddEntitlementsModule(this IServiceCollection services)
    {
        services.AddSingleton<ICorrelationContext, EntitlementsTestCorrelation>();
        services.AddSingleton<IAuditLogWriter, InMemoryAuditLogWriter>();
        services.AddScoped<IPricingCatalogService, PricingCatalogService>();
        services.AddScoped<ITenantPricingResolver, TenantPricingResolver>();
        services.AddScoped<IEntitlementsService, EntitlementsService>();
        return services;
    }

    private sealed class EntitlementsTestCorrelation : ICorrelationContext
    {
        public string CorrelationId { get; } = "test";

        public void Set(string correlationId) { }
    }
}
