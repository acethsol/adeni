namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class DiscoveryIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DiscoveryIntegrationTests(WebApplicationFactory<Program> factory) =>
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
        });

    [Fact]
    public async Task Discovery_and_public_profile_return_verified_business()
    {
        var slug = $"discovery-test-{Guid.NewGuid():N}"[..24].TrimEnd('-');

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            var tenantId = Guid.NewGuid();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Discovery Test Salon",
                Status = TenantStatus.Verified,
                CreatedAt = DateTimeOffset.UtcNow,
                VerifiedAt = DateTimeOffset.UtcNow
            });
            db.BusinessProfiles.Add(new BusinessProfile
            {
                TenantId = tenantId,
                CategorySlug = "hair-salons",
                Phone = "+2348099999999",
                Description = "Visible in discovery",
                UpdatedAt = DateTimeOffset.UtcNow
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Slug = slug,
                Name = "Lekki",
                MarketId = "lagos",
                AddressLine = "1 Test Road",
                Area = "Lekki",
                Latitude = 6.4474,
                Longitude = 3.4700,
                IsPrimary = true,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();

        var discoveryResponse = await client.GetAsync(
            $"/api/v1/discovery?lat=6.4474&lng=3.4700&category=hair-salons");
        Assert.Equal(HttpStatusCode.OK, discoveryResponse.StatusCode);

        var discovery = await discoveryResponse.Content.ReadFromJsonAsync<DiscoveryPayload>();
        Assert.NotNull(discovery);
        Assert.Contains(discovery!.Items, i => i.Slug == slug);

        var profileResponse = await client.GetAsync($"/api/v1/businesses/{slug}");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        var profile = await profileResponse.Content.ReadFromJsonAsync<PublicProfilePayload>();
        Assert.NotNull(profile);
        Assert.Equal("Discovery Test Salon", profile!.Name);
        Assert.Contains("[REDACTED]", profile.PhoneMasked);
    }

    private sealed record DiscoveryPayload(
        IReadOnlyList<DiscoveryItemPayload> Items,
        int Page,
        int PageSize,
        int TotalCount);

    private sealed record DiscoveryItemPayload(
        Guid TenantId,
        string Name,
        string Slug,
        string CategorySlug,
        string Area,
        double DistanceKm);

    private sealed record PublicProfilePayload(
        Guid TenantId,
        string Name,
        string Slug,
        string PhoneMasked);
}
