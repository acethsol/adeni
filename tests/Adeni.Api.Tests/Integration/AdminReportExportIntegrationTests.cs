namespace Adeni.Api.Tests.Integration;

using System.Net;
using Adeni.Api.Middleware;
using Adeni.Application.Reporting;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class AdminReportExportIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AdminReportExportIntegrationTests(WebApplicationFactory<Program> factory) =>
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
                services.Replace(ServiceDescriptor.Singleton<IAdminReportExportQueryService, StubAdminReportExportQueryService>());
            });
        });

    [Fact]
    public async Task BusinessDirectory_csv_returns_ok_for_admin()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            DevBusinessAuthMiddleware.DevAuth0SubHeader,
            DevelopmentDataSeeder.DevAdminAuth0Sub);

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/admin/reporting/exports/{AdminReportKeys.BusinessDirectory}?from={from}&to={to}&format=csv");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task BusinessDirectory_pdf_returns_bad_request()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            DevBusinessAuthMiddleware.DevAuth0SubHeader,
            DevelopmentDataSeeder.DevAdminAuth0Sub);

        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var response = await client.GetAsync(
            $"/api/v1/admin/reporting/exports/{AdminReportKeys.BusinessDirectory}?from={from}&to={to}&format=pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class StubAdminReportExportQueryService : IAdminReportExportQueryService
    {
        public Task<Result<IReadOnlyList<AdminBusinessDirectoryExportRow>>> GetBusinessDirectoryAsync(
            string? search,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<AdminBusinessDirectoryExportRow>>([]));

        public Task<Result<IReadOnlyList<AdminPendingVerificationExportRow>>> GetPendingVerificationsAsync(
            string? search,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<AdminPendingVerificationExportRow>>([]));

        public Task<Result<IReadOnlyList<AdminTransactionExportRow>>> GetTransactionsAsync(
            string? search,
            string? kind,
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<AdminTransactionExportRow>>([]));

        public Task<Result<IReadOnlyList<AdminPricingCatalogEntryExportRow>>> GetPricingCatalogEntriesAsync(
            string catalogVersionCode,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<AdminPricingCatalogEntryExportRow>>([]));
    }
}
