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
        var apiDir = ResolveApiDirectory();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiDir)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("AdeniDb")
            ?? "Server=localhost,1433;Database=adeni;User Id=sa;Password=Adeni_Dev_Passw0rd!;TrustServerCertificate=True;Encrypt=False";

        var optionsBuilder = new DbContextOptionsBuilder<AdeniDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new AdeniDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }

    private static string ResolveApiDirectory()
    {
        var cwd = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(cwd, "src", "Adeni.Api"),
            Path.Combine(cwd, "..", "Adeni.Api"),
            Path.Combine(cwd, "..", "..", "src", "Adeni.Api"),
            Path.Combine(cwd, "..", "..", "..", "src", "Adeni.Api"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Adeni.Api")),
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(full, "appsettings.Development.json")))
            {
                return full;
            }
        }

        return Path.GetFullPath(Path.Combine(cwd, "src", "Adeni.Api"));
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
