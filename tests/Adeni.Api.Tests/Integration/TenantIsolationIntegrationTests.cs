namespace Adeni.Api.Tests.Integration;

using System.Net;
using Adeni.Api.Middleware;
using Adeni.Application.Abstractions;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class TenantIsolationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TenantIsolationIntegrationTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty,
                    ["Redis:ConnectionString"] = string.Empty
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IAuditLogWriter, InMemoryAuditLogWriter>();
            });
        });

    [Fact]
    public async Task Tenant_cannot_access_other_tenant_services()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty,
                    ["Redis:ConnectionString"] = string.Empty
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IAuditLogWriter, InMemoryAuditLogWriter>();
                services.AddScoped<ICurrentUser>(_ => new TestCurrentUser("auth0|owner-a", tenantA));
            });
        });

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenantWithService(db, tenantA, "auth0|owner-a", "service-a");
            SeedTenantWithService(db, tenantB, "auth0|owner-b", "service-b");
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantB.ToString());

        var response = await client.GetAsync("/api/v1/tenant/services");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_can_access_own_services()
    {
        var tenantA = Guid.NewGuid();

        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty,
                    ["Redis:ConnectionString"] = string.Empty
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IAuditLogWriter, InMemoryAuditLogWriter>();
                services.AddScoped<ICurrentUser>(_ => new TestCurrentUser("auth0|owner-a", tenantA));
            });
        });

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            SeedTenantWithService(db, tenantA, "auth0|owner-a", "service-a");
        }

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantA.ToString());

        var response = await client.GetAsync("/api/v1/tenant/services");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static void SeedTenantWithService(
        AdeniDbContext db,
        Guid tenantId,
        string auth0Sub,
        string serviceName)
    {
        var now = DateTimeOffset.UtcNow;

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = serviceName,
            Status = TenantStatus.Verified,
            CreatedAt = now,
            VerifiedAt = now
        });

        db.BusinessUsers.Add(new BusinessUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Auth0Sub = auth0Sub,
            Role = "owner",
            CreatedAt = now
        });

        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "barbers",
            Phone = "+2348012345678",
            UpdatedAt = now
        });

        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = $"svc-{tenantId:N}"[..12],
            Name = "Main",
            MarketId = "lagos",
            AddressLine = "Test",
            Area = "Lekki",
            IsPrimary = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });

        db.ServiceOfferings.Add(new ServiceOffering
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = serviceName,
            DurationMinutes = 30,
            PriceAmount = 1000,
            Currency = "NGN",
            IsActive = true,
            CreatedAt = now
        });

        db.SaveChanges();
    }

    private sealed record TestCurrentUser(string UserId, Guid TenantGuid) : ICurrentUser
    {
        public IReadOnlyCollection<string> Roles { get; } = ["business"];
        public Domain.Tenancy.TenantId? TenantId => Domain.Tenancy.TenantId.Create(TenantGuid).Value;
        public bool HasMfa => false;
    }
}
