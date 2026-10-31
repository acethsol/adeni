namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class CustomerVerificationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CustomerVerificationIntegrationTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty,
                    ["Redis:ConnectionString"] = string.Empty,
                    ["CustomerVerification:PhoneVerificationHmacKey"] = "integration-test-key",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.PostConfigure<Adeni.Application.Identity.CustomerVerificationOptions>(options =>
                {
                    options.AutoVerifyCustomerInTesting = false;
                });
            });
        });

    [Fact]
    public async Task GetMeVerification_ReturnsStateForVerifiedCustomer()
    {
        const string sub = "auth0|verified-read";
        var verifiedAt = DateTimeOffset.UtcNow.AddDays(-1);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            db.Customers.Add(new Customer
            {
                Id = Guid.NewGuid(),
                Auth0Sub = sub,
                Name = "Verified",
                Email = "verified@example.com",
                Phone = "+16135550100",
                CreatedAt = DateTimeOffset.UtcNow,
                EmailVerifiedAt = verifiedAt,
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub", sub);

        var response = await client.GetAsync("/api/v1/me/verification");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<VerificationPayload>();
        Assert.NotNull(payload);
        Assert.True(payload!.EmailVerified);
    }

    private sealed record VerificationPayload(
        bool EmailVerified,
        bool PhoneVerified,
        string? EmailVerifiedAt,
        string? PhoneVerifiedAt);
}
