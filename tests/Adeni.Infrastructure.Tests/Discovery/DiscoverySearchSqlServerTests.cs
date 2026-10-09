namespace Adeni.Infrastructure.Tests.Discovery;

using Adeni.Application.Caching;
using Adeni.Application.Catalog;
using Adeni.Application.Discovery;
using Adeni.Application.Markets;
using Adeni.Application.Reviews;
using Adeni.Application.Storage;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Caching;
using Adeni.Infrastructure.Catalog;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Discovery;
using Adeni.Infrastructure.Markets;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Reviews;
using Adeni.Infrastructure.Tests.Catalog;
using Adeni.Infrastructure.Tests.Storage;
using Adeni.Infrastructure.Tests.TestData;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Hits <c>tenancy.DiscoverySearch</c> on a real SQL Server.
/// Opt in: <c>ADENI_SQLSERVER_TESTS=1</c> (and publish via <c>scripts/publish-db.ps1</c>).
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class DiscoverySearchSqlServerTests
{
    [SqlServerFact]
    public async Task Search_via_stored_procedure_returns_verified_distance_page()
    {
        await using var provider = BuildSqlServerProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var marker = $"SqlDist{suffix}";
        var nearSlug = $"sqltest-near-{suffix}";
        var farSlug = $"sqltest-far-{suffix}";
        var draftSlug = $"sqltest-draft-{suffix}";

        BusinessTestSeed.SeedVerifiedBusiness(db, nearSlug, $"{marker} Near", "barbers", "Lekki", 6.4474, 3.4700);
        BusinessTestSeed.SeedVerifiedBusiness(db, farSlug, $"{marker} Far", "barbers", "Ajah", 6.4698, 3.5852);
        BusinessTestSeed.SeedDraftBusiness(db, draftSlug, $"{marker} Draft", "barbers", "Lekki", 6.4474, 3.4700);
        await db.SaveChangesAsync();

        try
        {
            var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
            var result = await service.SearchAsync(
                6.4474,
                3.4700,
                categorySlug: "barbers",
                marketId: "lagos",
                query: marker,
                page: 1,
                pageSize: 20,
                sort: DiscoverySort.Distance);

            Assert.True(result.IsSuccess);
            var items = result.Value!.Items;
            Assert.Contains(items, i => i.Slug == nearSlug);
            Assert.Contains(items, i => i.Slug == farSlug);
            Assert.DoesNotContain(items, i => i.Slug == draftSlug);
            Assert.True(
                items.First(i => i.Slug == nearSlug).DistanceKm
                <= items.First(i => i.Slug == farSlug).DistanceKm);
        }
        finally
        {
            await CleanupAsync(db, nearSlug, farSlug, draftSlug);
        }
    }

    [SqlServerFact]
    public async Task Search_via_stored_procedure_supports_featured_sort_and_text()
    {
        await using var provider = BuildSqlServerProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var slug = $"sqltest-feat-{suffix}";
        BusinessTestSeed.SeedVerifiedBusiness(
            db,
            slug,
            $"UniqueFeat{suffix} Salon",
            "hair-salons",
            "VI",
            6.4281,
            3.4219);
        await db.SaveChangesAsync();

        try
        {
            var service = scope.ServiceProvider.GetRequiredService<DiscoveryService>();
            var result = await service.SearchAsync(
                6.4281,
                3.4219,
                categorySlug: null,
                marketId: "lagos",
                query: $"UniqueFeat{suffix}",
                page: 1,
                pageSize: 10,
                sort: DiscoverySort.Featured);

            Assert.True(result.IsSuccess);
            Assert.Contains(result.Value!.Items, i => i.Slug == slug);
        }
        finally
        {
            await CleanupAsync(db, slug);
        }
    }

    private static ServiceProvider BuildSqlServerProvider()
    {
        var connectionString = SqlServerTestGate.ConnectionString
            ?? throw new InvalidOperationException("SQL Server connection string missing.");

        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        services.AddSingleton<ICacheService, DistributedCacheService>();
        services.AddSingleton<MarketCatalogState>();
        services.AddSingleton<IMarketCatalog, SyncMarketCatalog>();
        services.AddCategoryWorkflowCatalog();
        services.AddScoped<TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddDbContext<AdeniDbContext>(o => o.UseSqlServer(connectionString));
        services.AddSingleton<IFileStorage, FakeFileStorage>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<DiscoveryService>();

        var provider = services.BuildServiceProvider();
        var catalogState = provider.GetRequiredService<MarketCatalogState>();
        catalogState.Update(
        [
            new MarketDefinition("lagos", "Lagos", "NG", "NGN", "Africa/Lagos", new MarketLocation(6.5244, 3.3792), ["en"], true, null),
            new MarketDefinition("ottawa", "Ottawa", "CA", "CAD", "America/Toronto", new MarketLocation(45.4215, -75.6972), ["en", "fr"], false, null),
        ]);
        return provider;
    }

    private static async Task CleanupAsync(AdeniDbContext db, params string[] slugs)
    {
        var locations = await db.BusinessLocations.Where(x => slugs.Contains(x.Slug)).ToListAsync();
        var tenantIds = locations.Select(x => x.TenantId).Distinct().ToList();
        if (tenantIds.Count == 0)
        {
            return;
        }

        db.BusinessLocations.RemoveRange(locations);
        db.BusinessProfiles.RemoveRange(db.BusinessProfiles.Where(x => tenantIds.Contains(x.TenantId)));
        db.Tenants.RemoveRange(db.Tenants.Where(x => tenantIds.Contains(x.Id)));
        await db.SaveChangesAsync();
    }
}

/// <summary>Skips unless <c>ADENI_SQLSERVER_TESTS=1</c> and SQL Server + proc are reachable.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
file sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!SqlServerTestGate.IsAvailable)
        {
            Skip = "Set ADENI_SQLSERVER_TESTS=1 and publish db (scripts/publish-db.ps1).";
        }
    }
}

file static class SqlServerTestGate
{
    public static string? ConnectionString { get; } = ResolveConnectionString();

    public static bool IsAvailable { get; } = Detect();

    private static string? ResolveConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__AdeniDb");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        return "Server=localhost,1433;Database=adeni;User Id=sa;Password=Adeni_Dev_Passw0rd!;TrustServerCertificate=True;Encrypt=False";
    }

    private static bool Detect()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("ADENI_SQLSERVER_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return false;
        }

        try
        {
            using var connection = new SqlConnection(ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT 1
                FROM sys.procedures p
                INNER JOIN sys.schemas s ON s.schema_id = p.schema_id
                WHERE s.name = N'tenancy' AND p.name = N'DiscoverySearch'
                """;
            return command.ExecuteScalar() is not null;
        }
        catch
        {
            return false;
        }
    }
}
