namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Adeni.Api.Middleware;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

public sealed class AdminPricingCatalogIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AdminPricingCatalogIntegrationTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty,
                });
            });
        });

    [Fact]
    public async Task ActivatePricingCatalogVersion_swaps_active_via_admin_api()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            DevBusinessAuthMiddleware.DevAuth0SubHeader,
            DevelopmentDataSeeder.DevAdminAuth0Sub);

        const string draftCode = "2026-integration-draft";
        var createPayload = JsonSerializer.Serialize(new
        {
            code = draftCode,
            effectiveFrom = "2026-11-01T00:00:00Z",
            effectiveTo = (string?)null,
            cloneFromActive = true,
        });
        var createResponse = await client.PostAsync(
            "/api/v1/admin/pricing-catalog/versions",
            new StringContent(createPayload, Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var activateResponse = await client.PostAsync(
            $"/api/v1/admin/pricing-catalog/versions/{draftCode}/activate",
            null);
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/v1/admin/pricing-catalog/versions");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listJson = await listResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(listJson);
        var activeCodes = doc.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Where(item => item.GetProperty("isActive").GetBoolean())
            .Select(item => item.GetProperty("code").GetString())
            .ToList();
        Assert.Single(activeCodes);
        Assert.Equal(draftCode, activeCodes[0]);

        var publicResponse = await client.GetAsync("/api/v1/pricing?countryCode=NG");
        var publicBody = await publicResponse.Content.ReadFromJsonAsync<PublicPricingResponse>();
        Assert.NotNull(publicBody);
        Assert.Equal(draftCode, publicBody.PricingCatalogVersion);
    }

    private sealed record PublicPricingResponse(
        string CountryCode,
        string CurrencyCode,
        string PricingCatalogVersion,
        bool SelfServeEnabled,
        decimal PlatformFeePercent,
        IReadOnlyList<object> Skus);
}
