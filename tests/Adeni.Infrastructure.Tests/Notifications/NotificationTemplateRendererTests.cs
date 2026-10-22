namespace Adeni.Infrastructure.Tests.Notifications;

using Adeni.Application.Abstractions;
using Adeni.Application.Notifications;
using Adeni.Domain.Notifications;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Notifications;
using Adeni.Infrastructure.Persistence;
using Adeni.Application.Subscriptions;
using Adeni.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TenantId = Adeni.Domain.Tenancy.TenantId;

public sealed class NotificationTemplateRendererTests
{
    [Fact]
    public async Task RenderAsync_uses_french_defaults_when_locale_fr()
    {
        await using var db = CreateDb();
        var renderer = CreateRenderer(db);

        var result = await renderer.RenderAsync(
            NotificationTemplateIds.BookingConfirmed,
            tenantId: null,
            locale: "fr",
            new Dictionary<string, string>
            {
                ["bookingId"] = "abc",
                ["startAtUtc"] = "2026-10-22T15:00:00Z",
            });

        Assert.True(result.IsSuccess);
        Assert.Equal("Réservation confirmée", result.Value!.Subject);
        Assert.Contains("abc", result.Value.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void MergeBilingualEmail_combines_english_and_french_sections()
    {
        var merged = NotificationTemplateText.MergeBilingualEmail(
            new RenderedNotificationTemplate("Confirmed", "Hello"),
            new RenderedNotificationTemplate("Confirmé", "Bonjour"));

        Assert.Equal("Confirmed / Confirmé", merged.Subject);
        Assert.Contains(NotificationTemplateText.BilingualEmailEnglishHeading, merged.Body, StringComparison.Ordinal);
        Assert.Contains("Hello", merged.Body, StringComparison.Ordinal);
        Assert.Contains(NotificationTemplateText.BilingualEmailFrenchHeading, merged.Body, StringComparison.Ordinal);
        Assert.Contains("Bonjour", merged.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderAsync_merges_tenant_override_when_configurableReminders()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Salon",
            SubscriptionSku = SubscriptionSku.Professional,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.TenantNotificationTemplateOverrides.Add(new TenantNotificationTemplateOverride
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TemplateId = NotificationTemplateIds.BookingConfirmed,
            Locale = "en",
            SubjectOverride = "Custom subject",
            BodyOverride = "Custom body for {bookingId}",
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var renderer = CreateRenderer(db);
        var result = await renderer.RenderAsync(
            NotificationTemplateIds.BookingConfirmed,
            tenantId,
            "en",
            new Dictionary<string, string> { ["bookingId"] = "xyz" });

        Assert.True(result.IsSuccess);
        Assert.Equal("Custom subject", result.Value!.Subject);
        Assert.Equal("Custom body for xyz", result.Value.Body);
    }

    private static NotificationTemplateRenderer CreateRenderer(AdeniDbContext db)
    {
        var host = new TestHost { ContentRootPath = AppContext.BaseDirectory };
        return new NotificationTemplateRenderer(host, db, new StubEntitlements());
    }

    private sealed class StubEntitlements : IEntitlementsService
    {
        private static readonly TenantEntitlements Pro =
            SubscriptionEntitlements.ForSku(SubscriptionSku.Professional);

        public TenantEntitlements GetEntitlementsForTenant(Tenant tenant) => Pro;

        public Task<int> GetBookingsUsedThisMonthAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<int> GetActiveLocationCountAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public Task<int> GetActivePractitionerCountAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public Task<Result> EnsureCanCreateBookingForTenantAsync(Tenant tenant, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> EnsureCanAddLocationForTenantAsync(
            Tenant tenant,
            int activeLocationCount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> EnsureCanAddPractitionerForTenantAsync(
            Tenant tenant,
            int activePractitionerCount,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> EnsureCanUseMessagingForTenantAsync(Tenant tenant, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> EnsureCanUseWaitlistForTenantAsync(Tenant tenant, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<SubscriptionUsageResponse> GetUsageForTenantAsync(Tenant tenant, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<TenantEntitlements>> GetEntitlementsForMemberAsync(
            Guid tenantId,
            string auth0Sub,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(Pro));
    }

    private static AdeniDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AdeniDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AdeniDbContext(options, new TestTenantContext());
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TenantId? CurrentTenantId => null;
        public bool IsTenantFilterActive => false;
        public void Clear() { }
        public void DisableTenantFilter() { }
        public void EnableTenantFilter(TenantId tenantId) { }
        public void Set(TenantId tenantId) { }
    }

    private sealed class TestHost : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
