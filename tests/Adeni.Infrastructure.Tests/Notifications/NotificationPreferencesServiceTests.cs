namespace Adeni.Infrastructure.Tests.Notifications;

using Adeni.Application.Abstractions;
using Adeni.Application.Notifications;
using Adeni.Domain.Auditing;
using Adeni.Domain.Identity;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Auditing;
using Adeni.Infrastructure.Notifications;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class NotificationPreferencesServiceTests
{
    [Fact]
    public async Task UpdateAsync_writes_tenant_notification_preferences_audit()
    {
        await using var provider = NotificationDispatcherTests.BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        const string auth0Sub = "auth0|prefs-audit-owner";
        var now = DateTimeOffset.UtcNow;
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Prefs Audit",
            Status = TenantStatus.Verified,
            SubscriptionSku = SubscriptionSku.Professional,
            CreatedAt = now,
            VerifiedAt = now,
        });
        db.BusinessUsers.Add(new BusinessUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Auth0Sub = auth0Sub,
            Role = "owner",
            CreatedAt = now,
        });
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<INotificationPreferencesService>();
        var result = await service.UpdateAsync(
            tenantId,
            new UpdateNotificationPreferencesRequest(
                EmailEnabled: true,
                PushEnabled: false,
                SmsRemindersEnabled: true,
                DefaultNotificationLocale: "en",
                SecondaryNotificationLocale: null,
                NotificationLanguageMode: "single",
                TemplateOverrides: []),
            auth0Sub);

        Assert.True(result.IsSuccess);

        var writer = (InMemoryAuditLogWriter)scope.ServiceProvider.GetRequiredService<IAuditLogWriter>();
        Assert.Contains(
            writer.Entries,
            e => e.Action == AuditActions.TenantNotificationPreferencesUpdated
                && e.EntityType == "tenant"
                && e.EntityId == tenantId.ToString()
                && e.ActorId == auth0Sub);
    }
}
