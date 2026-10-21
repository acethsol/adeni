namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Api.Middleware;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using BusinessProfile = Adeni.Domain.Tenancy.BusinessProfile;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class ListSortIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ListSortIntegrationTests(WebApplicationFactory<Program> factory) =>
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
    public async Task TenantCustomers_sortBy_name_returns_ordered_page()
    {
        var tenantId = Guid.NewGuid();
        const string ownerSub = "auth0|list-sort-customers";
        var now = DateTimeOffset.UtcNow;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Sort Salon",
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
            db.BusinessProfiles.Add(new BusinessProfile
            {
                TenantId = tenantId,
                CategorySlug = "barbers",
                Phone = "+2348012345678",
                UpdatedAt = now,
            });
            db.TenantCustomers.AddRange(
                new TenantCustomer
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    DisplayName = "Zara",
                    NormalizedPhone = "+15550001",
                    LastSeenAt = now.AddDays(-1),
                    CreatedAt = now,
                },
                new TenantCustomer
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    DisplayName = "Alice",
                    NormalizedPhone = "+15550002",
                    LastSeenAt = now,
                    CreatedAt = now,
                });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, ownerSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var response = await client.GetAsync(
            "/api/v1/tenant/customers?page=1&pageSize=10&sortBy=name&sortDir=asc");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<TenantCustomerListPayload>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload!.Items.Count);
        Assert.Equal("Alice", payload.Items[0].DisplayName);
        Assert.Equal("Zara", payload.Items[1].DisplayName);
    }

    private sealed record TenantCustomerListPayload(
        IReadOnlyList<TenantCustomerItemPayload> Items,
        int TotalCount,
        int Page,
        int PageSize);

    private sealed record TenantCustomerItemPayload(string DisplayName);
}
