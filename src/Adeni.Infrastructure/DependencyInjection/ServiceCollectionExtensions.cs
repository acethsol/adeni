namespace Adeni.Infrastructure.DependencyInjection;

using Adeni.Application.Abstractions;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Auth;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
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
        else
        {
            services.AddSingleton<IAuditLogWriter, InMemoryAuditLogWriter>();
        }

        services.AddAdeniAuth(configuration);

        return services;
    }
}
