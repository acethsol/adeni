namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Adeni.Api.Middleware;
using Adeni.Application.Tenancy;
using Adeni.Domain.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

public sealed class TenantOnboardingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TenantOnboardingIntegrationTests(WebApplicationFactory<Program> factory) =>
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
    public async Task Register_submit_and_profile_flow_works_in_testing()
    {
        const string auth0Sub = "auth0|integration-owner";
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, auth0Sub);

        var registerResponse = await client.PostAsJsonAsync("/api/v1/tenant/register", new RegisterBusinessRequest(
            "Integration Salon",
            "integration-salon",
            "hair-salons",
            "+2348098765432",
            "21 Ozumba Mbadiwe",
            "Victoria Island",
            "Test salon",
            null,
            null));

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterBusinessResponse>();
        Assert.NotNull(registered);

        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, registered.TenantId.ToString());

        var verificationResponse = await client.PostAsJsonAsync("/api/v1/tenant/verification", new SubmitVerificationRequest(
        [
            new(VerificationDocumentType.Cac, "RC999888")
        ]));
        Assert.Equal(HttpStatusCode.NoContent, verificationResponse.StatusCode);

        var profileResponse = await client.GetAsync("/api/v1/tenant/profile");
        Assert.Equal(HttpStatusCode.OK, profileResponse.StatusCode);

        var profileJson = await profileResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)TenantStatus.PendingVerification, profileJson.GetProperty("status").GetInt32());
    }
}
