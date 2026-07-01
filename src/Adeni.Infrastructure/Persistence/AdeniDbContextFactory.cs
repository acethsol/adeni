namespace Adeni.Infrastructure.Persistence;

using Adeni.Application.Abstractions;
using Adeni.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

public sealed class AdeniDbContextFactory : IDesignTimeDbContextFactory<AdeniDbContext>
{
    public AdeniDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("AdeniDb")
            ?? "Host=localhost;Port=5432;Database=adeni;Username=adeni;Password=adeni_dev_password";

        var optionsBuilder = new DbContextOptionsBuilder<AdeniDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__ef_migrations_history", "admin"));

        return new AdeniDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public TenantId? CurrentTenantId => null;
        public bool IsTenantFilterActive => false;
        public void EnableTenantFilter(TenantId tenantId) { }
        public void DisableTenantFilter() { }
        public void Set(TenantId tenantId) { }
        public void Clear() { }
    }
}
