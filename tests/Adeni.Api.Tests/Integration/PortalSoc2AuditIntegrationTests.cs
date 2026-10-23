namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Api.Middleware;
using Adeni.Application.Abstractions;
using Adeni.Application.Tenancy;
using Adeni.Domain.Auditing;
using Adeni.Domain.Identity;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class PortalSoc2AuditIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PortalSoc2AuditIntegrationTests(WebApplicationFactory<Program> factory) =>
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
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAuditLogWriter>();
                services.AddSingleton<IAuditLogWriter, InMemoryAuditLogWriter>();
            });
        });

    [Fact]
    public async Task Verification_submit_writes_business_verification_submitted_audit()
    {
        var slug = $"audit-ver-{Guid.NewGuid():N}"[..22].TrimEnd('-');
        var auth0Sub = $"auth0|audit-ver-{Guid.NewGuid():N}";
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, auth0Sub);

        var registerResponse = await client.PostAsJsonAsync("/api/v1/tenant/register", new RegisterBusinessRequest(
            "Audit Verification Salon",
            "hair-salons",
            "+2348098765432",
            new BusinessLocationRequest(slug, "VI", "1 Test Rd", "VI", "lagos", null, null),
            "Test"));
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<RegisterBusinessResponse>();
        Assert.NotNull(registered);

        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, registered.TenantId.ToString());

        var verificationResponse = await client.PostAsJsonAsync(
            "/api/v1/tenant/verification",
            new SubmitVerificationRequest([new(VerificationDocumentType.Cac, "RC111222")]));
        Assert.Equal(HttpStatusCode.NoContent, verificationResponse.StatusCode);

        var writer = _factory.Services.GetRequiredService<IAuditLogWriter>() as InMemoryAuditLogWriter;
        Assert.NotNull(writer);
        Assert.Contains(
            writer!.Entries,
            e => e.Action == AuditActions.BusinessVerificationSubmitted
                && e.EntityType == "tenant"
                && e.EntityId == registered.TenantId.ToString()
                && e.ActorId == auth0Sub);
    }

    [Fact]
    public async Task Settings_patch_writes_tenant_settings_updated_audit()
    {
        const string businessSub = "auth0|audit-settings-business";
        var tenantId = await SeedTenantAsync(businessSub);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, businessSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var patch = await client.PatchAsJsonAsync(
            "/api/v1/tenant/settings",
            new { autoConfirmBookings = true, depositPercent = 25 });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var writer = _factory.Services.GetRequiredService<IAuditLogWriter>() as InMemoryAuditLogWriter;
        Assert.NotNull(writer);
        Assert.Contains(
            writer!.Entries,
            e => e.Action == AuditActions.TenantSettingsUpdated
                && e.EntityType == "tenant"
                && e.EntityId == tenantId.ToString()
                && e.ActorId == businessSub);
    }

    [Fact]
    public async Task Notification_preferences_patch_writes_tenant_notification_preferences_audit()
    {
        const string businessSub = "auth0|audit-prefs-business";
        var tenantId = await SeedTenantAsync(businessSub);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevBusinessAuthMiddleware.DevAuth0SubHeader, businessSub);
        client.DefaultRequestHeaders.Add(TenantAccessMiddleware.TenantHeaderName, tenantId.ToString());

        var patch = await client.PatchAsJsonAsync(
            "/api/v1/tenant/notification-preferences",
            new
            {
                emailEnabled = true,
                pushEnabled = false,
                smsRemindersEnabled = true,
                defaultNotificationLocale = "en",
                notificationLanguageMode = "single",
                templateOverrides = Array.Empty<object>(),
            });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);

        var writer = _factory.Services.GetRequiredService<IAuditLogWriter>() as InMemoryAuditLogWriter;
        Assert.NotNull(writer);
        Assert.Contains(
            writer!.Entries,
            e => e.Action == AuditActions.TenantNotificationPreferencesUpdated
                && e.EntityType == "tenant"
                && e.EntityId == tenantId.ToString()
                && e.ActorId == businessSub);
    }

    private async Task<Guid> SeedTenantAsync(string businessSub)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Audit Prefs Shop",
            Status = TenantStatus.Verified,
            SubscriptionSku = SubscriptionSku.Professional,
            BillingStatus = TenantBillingStatus.Active,
            CreatedAt = now,
            VerifiedAt = now,
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = "barbers",
            Phone = "+2348012345678",
            UpdatedAt = now,
        });
        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = $"audit-prefs-{tenantId:N}"[..24],
            Name = "Main",
            MarketId = "lagos",
            AddressLine = "1 Test St",
            Area = "Lekki",
            TimeZoneId = "Africa/Lagos",
            IsPrimary = true,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        });
        db.BusinessUsers.Add(new BusinessUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Auth0Sub = businessSub,
            Role = PortalPermissionRoles.Owner,
            CreatedAt = now,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }
}
