namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Domain.Identity;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
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
    public async Task Customer_can_create_conversation_and_business_on_pro_can_reply()
    {
        var tenantId = await SeedTenantAsync(SubscriptionTier.Pro, BusinessSub, "Messaging Pro Shop", "messaging-pro-shop");

        var customerClient = _factory.CreateClient();
        customerClient.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub", CustomerSub);

        var createResponse = await customerClient.PostAsJsonAsync(
            "/api/v1/messages/conversations",
            new { tenantId });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var conversation = await createResponse.Content.ReadFromJsonAsync<ConversationPayload>();
        Assert.NotNull(conversation);

        var clientMessageId = Guid.NewGuid().ToString("N");
        var customerMessage = await customerClient.PostAsJsonAsync(
            $"/api/v1/messages/conversations/{conversation!.Id}/messages",
            new { body = "How much is a fade?", clientMessageId });

        Assert.Equal(HttpStatusCode.Created, customerMessage.StatusCode);

        var duplicate = await customerClient.PostAsJsonAsync(
            $"/api/v1/messages/conversations/{conversation.Id}/messages",
            new { body = "How much is a fade?", clientMessageId });
        Assert.Equal(HttpStatusCode.Created, duplicate.StatusCode);

        var businessClient = _factory.CreateClient();
        businessClient.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub", BusinessSub);
        businessClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var reply = await businessClient.PostAsJsonAsync(
            $"/api/v1/tenant/messages/conversations/{conversation.Id}/messages",
            new { body = "Fades start at 5,000 NGN.", clientMessageId = Guid.NewGuid().ToString("N") });

        Assert.Equal(HttpStatusCode.Created, reply.StatusCode);

        var markRead = await businessClient.PostAsync(
            $"/api/v1/tenant/messages/conversations/{conversation.Id}/read",
            null);
        Assert.Equal(HttpStatusCode.NoContent, markRead.StatusCode);

        var unread = await businessClient.GetFromJsonAsync<UnreadPayload>(
            "/api/v1/tenant/messages/unread-count");
        Assert.NotNull(unread);
        Assert.Equal(0, unread!.Count);
    }

    [Fact]
    public async Task Free_tier_business_cannot_list_conversations()
    {
        var tenantId = await SeedTenantAsync(SubscriptionTier.Free, BusinessSub + "-free", "Free Shop", "messaging-free-shop");

        var businessClient = _factory.CreateClient();
        businessClient.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub", BusinessSub + "-free");
        businessClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var response = await businessClient.GetAsync("/api/v1/tenant/messages/conversations");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Public_whatsapp_link_returns_url()
    {
        await SeedTenantAsync(SubscriptionTier.Pro, BusinessSub + "-wa", "WA Shop", "messaging-wa-shop");

        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/v1/businesses/messaging-wa-shop/whatsapp-link");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<WhatsAppPayload>();
        Assert.NotNull(payload);
        Assert.StartsWith("https://wa.me/", payload!.Url);
    }

    [Fact]
    public async Task Faq_auto_responder_replies_as_bot_when_enabled()
    {
        var tenantId = await SeedTenantAsync(
            SubscriptionTier.Pro,
            BusinessSub + "-faq",
            "FAQ Shop",
            "messaging-faq-shop",
            faqEnabled: true);

        var customerClient = _factory.CreateClient();
        customerClient.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub", CustomerSub + "-faq");

        var createResponse = await customerClient.PostAsJsonAsync(
            "/api/v1/messages/conversations",
            new { tenantId });
        var conversation = await createResponse.Content.ReadFromJsonAsync<ConversationPayload>();
        Assert.NotNull(conversation);

        await customerClient.PostAsJsonAsync(
            $"/api/v1/messages/conversations/{conversation!.Id}/messages",
            new { body = "What is your price for a cut?", clientMessageId = Guid.NewGuid().ToString("N") });

        var detail = await customerClient.GetFromJsonAsync<ConversationDetailPayload>(
            $"/api/v1/messages/conversations/{conversation.Id}");
        Assert.NotNull(detail);
        Assert.Contains(detail!.Messages, m => m.SenderType == "bot");
    }

    [Fact]
    public async Task Notification_preferences_can_be_updated()
    {
        var tenantId = await SeedTenantAsync(
            SubscriptionTier.Pro,
            PrefsBusinessSub,
            "Prefs Shop",
            "messaging-prefs-shop");

        var businessClient = _factory.CreateClient();
        businessClient.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub", PrefsBusinessSub);
        businessClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());

        var patch = await businessClient.PatchAsJsonAsync(
            "/api/v1/tenant/notification-preferences",
            new
            {
                emailEnabled = true,
                pushEnabled = false,
                smsWhatsAppReminderEnabled = true,
            });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
    }

    private async Task<Guid> SeedTenantAsync(
        SubscriptionTier tier,
        string businessSub,
        string name,
        string slug,
        bool faqEnabled = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = name,
            Status = TenantStatus.Verified,
            SubscriptionTier = tier,
            SubscriptionSku = tier == SubscriptionTier.Free ? SubscriptionSku.Starter : SubscriptionSku.Professional,
            BillingStatus = TenantBillingStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "barbers",
            Phone = "+2348012345678",
            Description = "Messaging test",
            FaqAutoResponderEnabled = faqEnabled,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = slug,
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
            Auth0Sub = businessSub,
            Role = "owner",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private sealed record ConversationPayload(Guid Id, Guid TenantId, bool NeedsAttention);

    private sealed record UnreadPayload(int Count);

    private sealed record WhatsAppPayload(string Url, string? PhoneMasked);

    private sealed record ConversationDetailPayload(List<MessagePayload> Messages);

    private sealed record MessagePayload(string SenderType, string? Body);
}
