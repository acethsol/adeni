namespace Adeni.Infrastructure.Tests.Subscriptions;

using System.Runtime.CompilerServices;
using Adeni.Application.Abstractions;
using Adeni.Application.Subscriptions;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

internal static class EntitlementsTestServiceRegistration
{
    [ModuleInitializer]
    internal static void EnsurePlanEntitlementsMatrixLoaded() =>
        _ = new JsonPlanEntitlementsMatrix(new EntitlementsTestHostEnvironment());

    internal static IServiceCollection AddEntitlementsModule(this IServiceCollection services)
    {
        services.AddSingleton<ICorrelationContext, EntitlementsTestCorrelation>();
        services.AddSingleton<IAuditLogWriter, InMemoryAuditLogWriter>();
        services.AddSingleton<IHostEnvironment>(_ => new EntitlementsTestHostEnvironment());
        services.AddSingleton<IPlanEntitlementsMatrix, JsonPlanEntitlementsMatrix>();
        services.AddScoped<IPricingCatalogService, PricingCatalogService>();
        services.AddScoped<ITenantPricingResolver, TenantPricingResolver>();
        services.AddScoped<IEntitlementsService, EntitlementsService>();
        return services;
    }

    private sealed class EntitlementsTestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Adeni.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class EntitlementsTestCorrelation : ICorrelationContext
    {
        public string CorrelationId { get; } = "test";

        public void Set(string correlationId) { }
    }
}
