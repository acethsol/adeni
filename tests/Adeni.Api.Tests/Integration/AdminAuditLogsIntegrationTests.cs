namespace Adeni.Api.Tests.Integration;

using System.Net;
using Adeni.Api.Middleware;
using Adeni.Application.Abstractions;
using Adeni.Domain.Auditing;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class AdminAuditLogsIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AdminAuditLogsIntegrationTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty,
                    ["Redis:ConnectionString"] = string.Empty,
                    ["Auth0:Enabled"] = "false",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditLogWriter>();
                services.AddSingleton<IAuditLogWriter, InMemoryAuditLogWriter>();
            });
        });

    [Fact]
    public async Task List_writes_admin_audit_logs_read_and_returns_rows()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var writer = scope.ServiceProvider.GetRequiredService<IAuditLogWriter>();
            await writer.WriteAsync(new AuditEntry(
                Guid.NewGuid(),
                "auth0|seed-admin",
                AuditActions.BusinessApproved,
                "tenant",
                Guid.NewGuid().ToString(),
                "corr-seed",
                DateTimeOffset.UtcNow,
                null));
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            DevBusinessAuthMiddleware.DevAuth0SubHeader,
            DevelopmentDataSeeder.DevAdminAuth0Sub);

        var response = await client.GetAsync("/api/v1/admin/audit-logs?pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var memoryWriter = _factory.Services.GetRequiredService<IAuditLogWriter>() as InMemoryAuditLogWriter;
        Assert.NotNull(memoryWriter);
        Assert.Contains(memoryWriter!.Entries, e => e.Action == AuditActions.AdminAuditLogsRead);
    }
}
