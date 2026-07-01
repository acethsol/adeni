namespace Adeni.Infrastructure.DependencyInjection;

using Adeni.Application.Abstractions;
using Adeni.Application.Admin;
using Adeni.Application.Auth;
using Adeni.Application.Catalog;
using Adeni.Application.Discovery;
using Adeni.Application.Tenancy;
using Adeni.Infrastructure.Admin;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Auth;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Catalog;
using Adeni.Infrastructure.Discovery;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Identity;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddSingleton<ICorrelationContext, CorrelationContext>()
            .AddScoped<ITenantContext, TenantContext>();

        var connectionString = configuration.GetConnectionString("AdeniDb");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<AdeniDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", "admin")));
            services.AddScoped<IAuditLogWriter, EfAuditLogWriter>();
        }
        else if (environment.IsDevelopment() || environment.EnvironmentName == "Testing")
        {
            services.AddDbContext<AdeniDbContext>(options =>
                options.UseInMemoryDatabase($"adeni-{environment.EnvironmentName}"));
            services.AddScoped<IAuditLogWriter, EfAuditLogWriter>();
        }
        else
        {
            services.AddSingleton<IAuditLogWriter, InMemoryAuditLogWriter>();
        }

        services.AddAdeniCaching(configuration, environment);
        services.AddAdeniAuth(configuration);
        services.AddScoped<IAuthSyncService, AuthSyncService>();
        services.AddScoped<IAdminBusinessService, AdminBusinessService>();
        services.AddScoped<IBusinessOnboardingService, BusinessOnboardingService>();
        services.AddScoped<IDiscoveryService, DiscoveryService>();
        services.AddSingleton<ICategoryService, CategoryService>();

        return services;
    }
}
