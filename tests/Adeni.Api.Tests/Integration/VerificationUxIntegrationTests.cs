namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Adeni.Api.Middleware;
using Adeni.Application.Tenancy;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class VerificationUxIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public VerificationUxIntegrationTests(WebApplicationFactory<Program> factory) =>
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
    public async Task Submit_with_short_reference_returns_validation()
    {
        var slug = $"verify-{Guid.NewGuid():N}"[..20].TrimEnd('-');
        var auth0Sub = $"auth0|verify-short-{Guid.NewGuid():N}";
        var client = CreateOwnerClient(auth0Sub);
        var tenantId = await RegisterDraftAsync(client, slug, auth0Sub);

        var response = await client.PostAsJsonAsync("/api/v1/tenant/verification", new SubmitVerificationRequest(
        [
            new(VerificationDocumentType.Cac, "RC12")
        ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _ = tenantId;
    }

    [Fact]
    public async Task Profile_returns_rejection_reason_after_admin_reject()
    {
        var slug = $"verify-{Guid.NewGuid():N}"[..20].TrimEnd('-');
        var auth0Sub = $"auth0|verify-reject-{Guid.NewGuid():N}";
        var ownerClient = CreateOwnerClient(auth0Sub);
        var tenantId = await RegisterDraftAsync(ownerClient, slug, auth0Sub);

        var submit = await ownerClient.PostAsJsonAsync("/api/v1/tenant/verification", new SubmitVerificationRequest(
        [
            new(VerificationDocumentType.Cac, "RC12345")
        ]));
        Assert.Equal(HttpStatusCode.NoContent, submit.StatusCode);

        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add(
            DevBusinessAuthMiddleware.DevAuth0SubHeader,
            DevelopmentDataSeeder.DevAdminAuth0Sub);

        var reject = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/businesses/{tenantId}/reject",
            new { reason = "Reference numbers could not be verified against CAC records." });
        Assert.Equal(HttpStatusCode.NoContent, reject.StatusCode);

        var profileResponse = await ownerClient.GetAsync("/api/v1/tenant/profile");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);
        var profileJson = await profileResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)TenantStatus.Rejected, profileJson.GetProperty("status").GetInt32());
        Assert.Equal(
            "Reference numbers could not be verified against CAC records.",
            profileJson.GetProperty("verificationRejectionReason").GetString());
    }

    [Fact]
    public async Task Admin_reject_with_short_reason_returns_validation()
    {
        var adminClient = _factory.CreateClient();
        adminClient.DefaultRequestHeaders.Add(
            DevBusinessAuthMiddleware.DevAuth0SubHeader,
            DevelopmentDataSeeder.DevAdminAuth0Sub);

        var response = await adminClient.PostAsJsonAsync(
            $"/api/v1/admin/businesses/{Guid.NewGuid()}/reject",
            new { reason = "too short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private HttpClient CreateOwnerClient(string auth0Sub)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, auth0Sub);
        return client;
    }

    private static async Task<Guid> RegisterDraftAsync(HttpClient client, string slug, string auth0Sub)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/v1/tenant/register", new RegisterBusinessRequest(
            "Verify UX Salon",
            "hair-salons",
            "+2348098765432",
            new BusinessLocationRequest(
                slug,
                "Victoria Island",
                "21 Ozumba Mbadiwe",
                "Victoria Island",
                "lagos",
                null,
                null),
            "Test salon"));

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterBusinessResponse>();
        Assert.NotNull(registered);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, registered.TenantId.ToString());
        _ = auth0Sub;
        return registered.TenantId;
    }
}
