namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Domain.Booking;
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
    private const string PrefsBusinessSub = "auth0|messaging-prefs-business";

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

    [Fact]
    public async Task Faq_auto_responder_replies_to_pricing_question()
    {
        var tenantId = Guid.Empty;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            tenantId = Guid.NewGuid();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "FAQ Shop",
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
                FaqAutoResponderEnabled = true,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            db.ServiceOfferings.Add(new ServiceOffering
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = "Haircut",
                Currency = "NGN",
                PriceAmount = 5000m,
                DurationMinutes = 30,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var customerClient = _factory.CreateClient();
        customerClient.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub", CustomerSub);

        var createResponse = await customerClient.PostAsJsonAsync(
            "/api/v1/messages/threads",
            new { tenantId });

        var thread = await createResponse.Content.ReadFromJsonAsync<ThreadPayload>();
        Assert.NotNull(thread);

        await customerClient.PostAsJsonAsync(
            $"/api/v1/messages/threads/{thread!.Id}/messages",
            new { body = "How much does a haircut cost?" });

        var detailResponse = await customerClient.GetAsync(
            $"/api/v1/messages/threads/{thread.Id}");

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<ThreadDetailPayload>();
        Assert.NotNull(detail);
        Assert.True(detail!.Messages.Count >= 2);
        Assert.Contains(detail.Messages, message =>
            message.SenderType == "business" && message.Body.Contains("Haircut", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Tenant_can_update_notification_preferences()
    {
        var tenantId = Guid.Empty;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
            tenantId = Guid.NewGuid();
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Prefs Shop",
                Status = TenantStatus.Verified,
                CreatedAt = DateTimeOffset.UtcNow,
                VerifiedAt = DateTimeOffset.UtcNow,
            });
            db.BusinessUsers.Add(new BusinessUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Auth0Sub = PrefsBusinessSub,
                Role = "owner",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var businessClient = _factory.CreateClient();
        businessClient.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub", PrefsBusinessSub);
        businessClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var getDefaults = await businessClient.GetFromJsonAsync<NotificationPrefsPayload>(
            "/api/v1/tenant/notification-preferences");

        Assert.NotNull(getDefaults);
        Assert.True(getDefaults!.EmailEnabled);

        var patchResponse = await businessClient.PatchAsJsonAsync(
            "/api/v1/tenant/notification-preferences",
            new { emailEnabled = false, pushEnabled = true, smsWhatsAppReminderEnabled = true });

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
        var updated = await patchResponse.Content.ReadFromJsonAsync<NotificationPrefsPayload>();
        Assert.NotNull(updated);
        Assert.False(updated!.EmailEnabled);
        Assert.True(updated.PushEnabled);
        Assert.True(updated.SmsWhatsAppReminderEnabled);
    }

    private sealed record ThreadPayload(Guid Id);
    private sealed record UnreadPayload(int Count);
    private sealed record WhatsAppPayload(string Url);
    private sealed record ThreadDetailPayload(IReadOnlyList<MessagePayload> Messages);
    private sealed record MessagePayload(string SenderType, string Body);
    private sealed record NotificationPrefsPayload(
        bool EmailEnabled,
        bool PushEnabled,
        bool SmsWhatsAppReminderEnabled);
}
