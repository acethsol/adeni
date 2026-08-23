namespace Adeni.Infrastructure.DependencyInjection;

using Adeni.Application.Abstractions;
using Adeni.Application.Caching;
using Adeni.Application.Markets;
using Adeni.Infrastructure.Admin;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Auth;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Catalog;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Discovery;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Identity;
using Adeni.Infrastructure.Notifications;
using Adeni.Infrastructure.Payments;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Reviews;
using Adeni.Infrastructure.Markets;
using Adeni.Infrastructure.Subscriptions;
using Adeni.Infrastructure.Tenancy;
using Adeni.Infrastructure.Translation;
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
        services.AddAdeniDomainEvents();
        services.AddAdeniMarkets();
        services.AddAdeniTranslation(configuration, environment);
        services.Configure<MarketOptions>(configuration.GetSection(MarketOptions.SectionName));
        services.AddAdeniAuth(configuration);
        services.AddIdentityModule();
        services.AddAdminModule();
        services.AddTenancyModule();
        services.AddCatalogModule();
        services.AddBookingModule();
        services.AddReviewsModule();
        services.AddDiscoveryModule();
        services.AddNotificationsModule();
        services.AddPaymentsModule();
        services.AddSubscriptionsModule();

        return services;
    }
}
