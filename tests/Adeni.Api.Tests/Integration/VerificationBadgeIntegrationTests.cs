namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Adeni.Api.Middleware;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class VerificationBadgeIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public VerificationBadgeIntegrationTests(WebApplicationFactory<Program> factory) =>
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
        });

    [Fact]
    public async Task ListBadges_AfterRequest_ReturnsPendingWithReference()
    {
        var (client, _) = await CreateVerifiedOwnerAsync();

        var request = await client.PostAsJsonAsync(
            "/api/v1/tenant/verification/badges",
            new { badgeType = "cac", referenceNumber = "RC1234567" });
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);

        var items = await ListBadgesAsync(client);
        var cac = Assert.Single(items, x => x.GetProperty("badgeType").GetString() == "cac");
        Assert.Equal("pending", cac.GetProperty("status").GetString());
    }

    [Fact]
    public async Task RequestBadge_WhenAlreadyPending_ReturnsConflict()
    {
        var (client, _) = await CreateVerifiedOwnerAsync();
        var body = new { badgeType = "license" };

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/tenant/verification/badges", body)).StatusCode);
        var second = await client.PostAsJsonAsync("/api/v1/tenant/verification/badges", body);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task RequestBadge_AfterAdminRevoke_TransitionsRevokedToPending()
    {
        var (client, tenantId) = await CreateVerifiedOwnerAsync();

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/v1/tenant/verification/badges", new { badgeType = "cac", referenceNumber = "RC1111111" })).StatusCode);

        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Add(
            DevBusinessAuthMiddleware.DevAuth0SubHeader,
            DevelopmentDataSeeder.DevAdminAuth0Sub);

        var revoke = await admin.PostAsync(
            $"/api/v1/admin/verification/businesses/{tenantId}/badges/cac/revoke",
            content: null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        // Revoked rows are hidden from the tenant list so the portal offers "Request" again.
        Assert.DoesNotContain(await ListBadgesAsync(client), x => x.GetProperty("badgeType").GetString() == "cac");

        var again = await client.PostAsJsonAsync(
            "/api/v1/tenant/verification/badges",
            new { badgeType = "cac", referenceNumber = "RC2222222" });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);

        var cac = Assert.Single(await ListBadgesAsync(client), x => x.GetProperty("badgeType").GetString() == "cac");
        Assert.Equal("pending", cac.GetProperty("status").GetString());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var rows = await db.TenantVerificationBadges
            .IgnoreQueryFilters()
            .Where(x => x.TenantId == tenantId && x.BadgeType == VerificationBadgeType.Cac)
            .ToListAsync();
        var row = Assert.Single(rows);
        Assert.Equal(VerificationBadgeStatus.Pending, row.Status);
        Assert.Equal("RC2222222", row.ReferenceNumber);
        Assert.Null(row.GrantedAt);
        Assert.Null(row.GrantedByAdminId);
    }

    private static async Task<List<JsonElement>> ListBadgesAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/tenant/verification/badges");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("items").EnumerateArray().ToList();
    }

    private async Task<(HttpClient Client, Guid TenantId)> CreateVerifiedOwnerAsync()
    {
        var tenantId = Guid.NewGuid();
        var ownerSub = $"auth0|badge-owner-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Badge Test Salon",
                Status = TenantStatus.Verified,
                CreatedAt = now,
                VerifiedAt = now,
            });
            db.BusinessUsers.Add(new BusinessUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Auth0Sub = ownerSub,
                Role = "owner",
                CreatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());
        return (client, tenantId);
    }
}
