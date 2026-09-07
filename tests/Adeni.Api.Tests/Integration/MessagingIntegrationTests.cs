namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class MessagingIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string CustomerSub = "auth0|messaging-customer";
    private const string BusinessSub = "auth0|messaging-business";

    private readonly WebApplicationFactory<Program> _factory;

    public MessagingIntegrationTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AdeniDb"] = string.Empty,
                    ["Redis:ConnectionString"] = string.Empty,
                });
            });
        });

    [Fact]
    public async Task Customer_can_create_thread_and_business_on_pro_can_reply()
    {
        var tenantId = Guid.Empty;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            tenantId = Guid.NewGuid();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Messaging Pro Shop",
                Status = TenantStatus.Verified,
                SubscriptionTier = SubscriptionTier.Pro,
                CreatedAt = DateTimeOffset.UtcNow,
                VerifiedAt = DateTimeOffset.UtcNow,
            });
            db.BusinessProfiles.Add(new BusinessProfile
            {
                TenantId = tenantId,
                CategorySlug = "barbers",
                Phone = "+2348012345678",
                Description = "Messaging test",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Slug = "messaging-pro-shop",
                Name = "Main",
                MarketId = "lagos",
                AddressLine = "12 Test St",
                Area = "Lekki",
                TimeZoneId = "Africa/Lagos",
                IsPrimary = true,
                IsActive = true,
            });
            db.BusinessUsers.Add(new BusinessUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Auth0Sub = BusinessSub,
                Role = "owner",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var customerClient = _factory.CreateClient();
        customerClient.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub", CustomerSub);

        var createResponse = await customerClient.PostAsJsonAsync(
            "/api/v1/messages/threads",
            new { tenantId });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var thread = await createResponse.Content.ReadFromJsonAsync<ThreadPayload>();
        Assert.NotNull(thread);

        var customerMessage = await customerClient.PostAsJsonAsync(
            $"/api/v1/messages/threads/{thread!.Id}/messages",
            new { body = "Hi, do you have slots tomorrow?" });

        Assert.Equal(HttpStatusCode.Created, customerMessage.StatusCode);

        var businessClient = _factory.CreateClient();
        businessClient.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub", BusinessSub);
        businessClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var businessReply = await businessClient.PostAsJsonAsync(
            $"/api/v1/tenant/messages/threads/{thread.Id}/messages",
            new { body = "Yes — book online anytime." });

        Assert.Equal(HttpStatusCode.Created, businessReply.StatusCode);

        var unreadBeforeRead = await businessClient.GetFromJsonAsync<UnreadPayload>(
            "/api/v1/tenant/messages/unread-count");

        Assert.NotNull(unreadBeforeRead);
        Assert.Equal(1, unreadBeforeRead!.Count);

        var markRead = await businessClient.PostAsync(
            $"/api/v1/tenant/messages/threads/{thread.Id}/read",
            null);

        Assert.Equal(HttpStatusCode.NoContent, markRead.StatusCode);

        var unread = await businessClient.GetFromJsonAsync<UnreadPayload>(
            "/api/v1/tenant/messages/unread-count");

        Assert.NotNull(unread);
        Assert.Equal(0, unread!.Count);
    }

    [Fact]
    public async Task Free_tier_business_cannot_access_tenant_messages()
    {
        var tenantId = Guid.Empty;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            tenantId = Guid.NewGuid();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Free Tier Shop",
                Status = TenantStatus.Verified,
                SubscriptionTier = SubscriptionTier.Free,
                CreatedAt = DateTimeOffset.UtcNow,
                VerifiedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var businessClient = _factory.CreateClient();
        businessClient.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub", BusinessSub);
        businessClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await businessClient.GetAsync("/api/v1/tenant/messages/threads");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Public_whatsapp_link_is_returned_for_business_slug()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            var tenantId = Guid.NewGuid();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "WA Shop",
                Status = TenantStatus.Verified,
                CreatedAt = DateTimeOffset.UtcNow,
                VerifiedAt = DateTimeOffset.UtcNow,
            });
            db.BusinessProfiles.Add(new BusinessProfile
            {
                TenantId = tenantId,
                CategorySlug = "barbers",
                Phone = "+2348099999999",
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            db.BusinessLocations.Add(new BusinessLocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Slug = "wa-shop",
                Name = "Main",
                MarketId = "lagos",
                AddressLine = "1 WA St",
                Area = "VI",
                TimeZoneId = "Africa/Lagos",
                IsPrimary = true,
                IsActive = true,
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/businesses/wa-shop/whatsapp-link");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<WhatsAppPayload>();
        Assert.NotNull(payload);
        Assert.Contains("wa.me", payload!.Url, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ThreadPayload(Guid Id);
    private sealed record UnreadPayload(int Count);
    private sealed record WhatsAppPayload(string Url);
}
