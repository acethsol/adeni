namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Application.Subscriptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

public sealed class PricingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PricingIntegrationTests(WebApplicationFactory<Program> factory) =>
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
    public async Task GetPublicPricing_ng_returns_launch_catalog()
    {
        await PricingCatalogIntegrationTestState.EnsureLaunchCatalogActiveAsync(_factory.Services);

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/pricing?countryCode=NG");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PublicPricingResponse>();
        Assert.NotNull(body);
        Assert.Equal("NG", body.CountryCode);
        Assert.Equal(PricingCatalogMetadata.ActiveVersionCode, body.PricingCatalogVersion);
        Assert.Contains(body.Skus, s => s.Sku == "starter" && s.MonthlyAmountMajor == 35_000m);
    }

    private sealed record PublicSkuResponse(string Sku, decimal? MonthlyAmountMajor);

    private sealed record PublicPricingResponse(
        string CountryCode,
        string CurrencyCode,
        string PricingCatalogVersion,
        bool SelfServeEnabled,
        decimal PlatformFeePercent,
        IReadOnlyList<PublicSkuResponse> Skus);
}
