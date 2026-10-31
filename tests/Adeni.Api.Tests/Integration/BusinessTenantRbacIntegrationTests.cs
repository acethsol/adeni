namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Api.Middleware;
using Adeni.Application.Storage;
using Adeni.Application.Tenancy;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class BusinessTenantRbacIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public BusinessTenantRbacIntegrationTests(WebApplicationFactory<Program> factory) =>
        this.factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AdeniDb"] = "",
                ["Redis:ConnectionString"] = "",
                ["Auth0:Enabled"] = "false",
            }));
        });

    private async Task<(HttpClient Client, Guid TenantId, Guid CustomerRosterId)> SeedAsync(string role)
    {
        var tenantId = Guid.NewGuid();
        var rosterId = Guid.NewGuid();
        var platformId = Guid.NewGuid();
        var sub = $"auth0|rbac-{role}-{tenantId:N}";
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "RBAC tests",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "hair-salons",
            Phone = "+2348000000000",
        });
        db.BusinessUsers.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenantId, Auth0Sub = sub, Role = role });
        db.Customers.Add(new Customer { Id = platformId, Auth0Sub = $"cust-{platformId:N}", Name = "Ada" });
        db.TenantCustomers.Add(new TenantCustomer
        {
            Id = rosterId,
            TenantId = tenantId,
            CustomerId = platformId,
            DisplayName = "Ada",
        });
        await db.SaveChangesAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, sub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());
        return (client, tenantId, rosterId);
    }

    [Theory]
    [InlineData("practitioner")]
    [InlineData("accountant")]
    public async Task Practitioner_CannotMutateTenantProfile(string role)
    {
        var (client, _, _) = await SeedAsync(role);
        var response = await client.PatchAsJsonAsync(
            "/api/v1/tenant/profile",
            new UpdateBusinessProfileRequest("Hack", "hair-salons", "+2348000000001", null));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("practitioner")]
    public async Task Practitioner_CannotMutateLocationsOrVerification(string role)
    {
        var (client, _, _) = await SeedAsync(role);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            "/api/v1/tenant/locations",
            new UpsertBusinessLocationRequest("branch", "Branch", "1 St", "VI", "lagos", null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            "/api/v1/tenant/verification",
            new SubmitVerificationRequest([new(VerificationDocumentType.Cac, "RC999")]))).StatusCode);
    }

    [Fact]
    public async Task Practitioner_CannotListTenantCustomersOrStatement()
    {
        var (client, _, rosterId) = await SeedAsync("practitioner");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/tenant/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/tenant/customers/{rosterId}")).StatusCode);
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-30).ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(
            $"/api/v1/tenant/customers/{rosterId}/statement?from={from}&to={to}")).StatusCode);
    }

    [Fact]
    public async Task Receptionist_CanListCustomers_ButPractitionerCannot()
    {
        var receptionist = await SeedAsync("receptionist");
        Assert.Equal(HttpStatusCode.OK, (await receptionist.Client.GetAsync("/api/v1/tenant/customers")).StatusCode);
    }

    [Fact]
    public async Task Practitioner_CannotSetKioskPin()
    {
        var (client, _, _) = await SeedAsync("practitioner");
        var response = await client.PutAsJsonAsync("/api/v1/tenant/kiosk/pin", new { pin = "1234" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Owner_CanMutateProfileAndListCustomers()
    {
        var (client, _, rosterId) = await SeedAsync("owner");
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync(
            "/api/v1/tenant/profile",
            new UpdateBusinessProfileRequest("Owned Salon", "hair-salons", "+2348000000002", null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tenant/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/tenant/customers/{rosterId}")).StatusCode);
    }

    [Fact]
    public async Task Practitioner_CannotRequestVerificationUploadUrl()
    {
        var (client, _, _) = await SeedAsync("practitioner");
        var response = await client.PostAsJsonAsync(
            "/api/v1/tenant/media/upload-url",
            new MediaUploadUrlRequest("verification_document", "application/pdf", 1024));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
