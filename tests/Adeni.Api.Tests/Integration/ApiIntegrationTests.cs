namespace Adeni.Api.Tests.Integration;

using System.Net;
using Adeni.Api.Middleware;
using Adeni.Application.Abstractions;
using Adeni.Domain.Auditing;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class ApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiIntegrationTests(WebApplicationFactory<Program> factory) =>
        _factory = CreateTestFactory(factory);

    private static WebApplicationFactory<Program> CreateTestFactory(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IAuditLogWriter, InMemoryAuditLogWriter>();
            });
        });

    [Fact]
    public async Task Health_returns_ok_with_security_headers()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Tenant_route_without_header_returns_400()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/tenant/profile");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_route_cross_tenant_returns_403_and_audits()
    {
        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();

        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IAuditLogWriter, InMemoryAuditLogWriter>();
                services.AddScoped<ICurrentUser>(_ => new TestCurrentUser("user-1", tenant));
            });
        });

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, otherTenant.ToString());

        var response = await client.GetAsync("/api/v1/tenant/profile");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var writer = factory.Services.GetRequiredService<IAuditLogWriter>() as InMemoryAuditLogWriter;
        Assert.NotNull(writer);
        Assert.Contains(writer!.Entries, e => e.Action == AuditActions.CrossTenantDenied);
    }

    [Theory]
    [InlineData("/api/v1/tenant/profile", true)]
    [InlineData("/api/v1/tenant/register", false)]
    public void RequiresTenantHeader_matches_expected_routes(string path, bool expected)
    {
        Assert.Equal(expected, TenantAccessMiddleware.RequiresTenantHeader(path));
    }

    private sealed record TestCurrentUser(string UserId, Guid TenantGuid) : ICurrentUser
    {
        public IReadOnlyCollection<string> Roles { get; } = ["business"];
        public Domain.Tenancy.TenantId? TenantId => Domain.Tenancy.TenantId.Create(TenantGuid).Value;
        public bool HasMfa => false;
    }
}
