namespace Adeni.Api.Tests.Integration;

using System.Net;
using Adeni.Api.Middleware;
using Adeni.Application.Abstractions;
using Adeni.Application.Reporting;
using Adeni.Domain.Auditing;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class AdminReportingAuditIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AdminReportingAuditIntegrationTests(WebApplicationFactory<Program> factory) =>
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
                services.AddSingleton<IAuditLogWriter, InMemoryAuditLogWriter>();
                services.Replace(ServiceDescriptor.Singleton<IAdminReportingQueryService, StubAdminReportingQueryService>());
            });
        });

    [Fact]
    public async Task PlatformSubscriptionMrr_get_writes_admin_reporting_audit()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            DevBusinessAuthMiddleware.DevAuth0SubHeader,
            DevelopmentDataSeeder.DevAdminAuth0Sub);

        var response = await client.GetAsync("/api/v1/admin/reporting/platform-subscription-mrr");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var writer = _factory.Services.GetRequiredService<IAuditLogWriter>() as InMemoryAuditLogWriter;
        Assert.NotNull(writer);
        Assert.Contains(writer!.Entries, e => e.Action == AuditActions.AdminReportingRead);
    }

    private sealed class StubAdminReportingQueryService : IAdminReportingQueryService
    {
        public Task<PlatformSubscriptionMrrResponse> GetPlatformSubscriptionMrrAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PlatformSubscriptionMrrResponse(
                [],
                [],
                "estimated_list_price",
                0,
                0,
                0,
                DateTimeOffset.UtcNow));

        public Task<AdminGmvSummaryResponse> GetGmvAsync(
            string granularity = "month",
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdminGmvSummaryResponse(
                granularity,
                [],
                0,
                0,
                DateTimeOffset.UtcNow));
    }
}
