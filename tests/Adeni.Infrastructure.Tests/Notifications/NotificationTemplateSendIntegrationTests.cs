namespace Adeni.Infrastructure.Tests.Notifications;

using Adeni.Application.Notifications;
using Adeni.Domain.Booking;
using Adeni.Domain.Booking.Events;
using Adeni.Domain.Identity;
using Adeni.Domain.Notifications;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Notifications;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class NotificationTemplateSendIntegrationTests
{
    [Fact]
    public async Task BookingConfirmed_renders_french_subject_when_tenant_default_locale_fr()
    {
        await using var provider = NotificationDispatcherTests.BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Shop", CreatedAt = DateTimeOffset.UtcNow });
        db.Customers.Add(new Customer
        {
            Id = customerId,
            Auth0Sub = "auth0|fr",
            Name = "Guest",
            Email = "guest@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.TenantNotificationPreferences.Add(new TenantNotificationPreferences
        {
            TenantId = tenantId,
            EmailEnabled = true,
            PushEnabled = false,
            SmsRemindersEnabled = false,
            DefaultNotificationLocale = "fr",
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.Bookings.Add(new BookingRecord
        {
            Id = bookingId,
            TenantId = tenantId,
            CustomerId = customerId,
            ServiceOfferingId = Guid.NewGuid(),
            StartAt = DateTimeOffset.UtcNow.AddDays(1),
            EndAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var worker = scope.ServiceProvider.GetRequiredService<NotificationSendWorker>();
        var sink = scope.ServiceProvider.GetRequiredService<NotificationDispatcherTests.CapturingEmailProvider>();

        await worker.SendAsync(
            NotificationJobPayload.FromMessage(
                NotificationMessage.FromTemplate(
                    "email",
                    "guest@example.com",
                    NotificationTemplateIds.BookingConfirmed,
                    new Dictionary<string, string>
                    {
                        ["bookingId"] = bookingId.ToString("D"),
                        ["startAtUtc"] = DateTimeOffset.UtcNow.AddDays(1).ToString("o"),
                    },
                    tenantId,
                    $"booking-confirmed:{bookingId:D}",
                    customerId)));

        Assert.Single(sink.Sent);
        Assert.Equal("Réservation confirmée", sink.Sent[0].Subject);
    }

    [Fact]
    public async Task BookingConfirmed_email_includes_bilingual_sections_when_tenant_mode_bilingual()
    {
        await using var provider = NotificationDispatcherTests.BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Shop", CreatedAt = DateTimeOffset.UtcNow });
        db.Customers.Add(new Customer
        {
            Id = customerId,
            Auth0Sub = "auth0|bi",
            Name = "Guest",
            Email = "guest@example.com",
            PreferredLocale = "fr",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.TenantNotificationPreferences.Add(new TenantNotificationPreferences
        {
            TenantId = tenantId,
            EmailEnabled = true,
            PushEnabled = false,
            SmsRemindersEnabled = false,
            DefaultNotificationLocale = "en",
            SecondaryNotificationLocale = "fr",
            NotificationLanguageMode = "bilingual",
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.Bookings.Add(new BookingRecord
        {
            Id = bookingId,
            TenantId = tenantId,
            CustomerId = customerId,
            ServiceOfferingId = Guid.NewGuid(),
            StartAt = DateTimeOffset.UtcNow.AddDays(1),
            EndAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var worker = scope.ServiceProvider.GetRequiredService<NotificationSendWorker>();
        var sink = scope.ServiceProvider.GetRequiredService<NotificationDispatcherTests.CapturingEmailProvider>();

        await worker.SendAsync(
            NotificationJobPayload.FromMessage(
                NotificationMessage.FromTemplate(
                    "email",
                    "guest@example.com",
                    NotificationTemplateIds.BookingConfirmed,
                    new Dictionary<string, string>
                    {
                        ["bookingId"] = bookingId.ToString("D"),
                        ["startAtUtc"] = DateTimeOffset.UtcNow.AddDays(1).ToString("o"),
                    },
                    tenantId,
                    $"booking-confirmed:{bookingId:D}",
                    customerId)));

        Assert.Single(sink.Sent);
        Assert.Contains("--- English ---", sink.Sent[0].Body, StringComparison.Ordinal);
        Assert.Contains("--- Français ---", sink.Sent[0].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BookingConfirmed_email_bilingual_fr_primary_en_secondary_orders_french_first()
    {
        await using var provider = NotificationDispatcherTests.BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Shop", CreatedAt = DateTimeOffset.UtcNow });
        db.Customers.Add(new Customer
        {
            Id = customerId,
            Auth0Sub = "auth0|bi-fr",
            Name = "Guest",
            Email = "guest@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.TenantNotificationPreferences.Add(new TenantNotificationPreferences
        {
            TenantId = tenantId,
            EmailEnabled = true,
            PushEnabled = false,
            SmsRemindersEnabled = false,
            DefaultNotificationLocale = "fr",
            SecondaryNotificationLocale = "en",
            NotificationLanguageMode = "bilingual",
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.Bookings.Add(new BookingRecord
        {
            Id = bookingId,
            TenantId = tenantId,
            CustomerId = customerId,
            ServiceOfferingId = Guid.NewGuid(),
            StartAt = DateTimeOffset.UtcNow.AddDays(1),
            EndAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var worker = scope.ServiceProvider.GetRequiredService<NotificationSendWorker>();
        var sink = scope.ServiceProvider.GetRequiredService<NotificationDispatcherTests.CapturingEmailProvider>();

        await worker.SendAsync(
            NotificationJobPayload.FromMessage(
                NotificationMessage.FromTemplate(
                    "email",
                    "guest@example.com",
                    NotificationTemplateIds.BookingConfirmed,
                    new Dictionary<string, string>
                    {
                        ["bookingId"] = bookingId.ToString("D"),
                        ["startAtUtc"] = DateTimeOffset.UtcNow.AddDays(1).ToString("o"),
                    },
                    tenantId,
                    $"booking-confirmed:{bookingId:D}",
                    customerId)));

        Assert.Single(sink.Sent);
        var body = sink.Sent[0].Body;
        var frenchIndex = body.IndexOf("--- Français ---", StringComparison.Ordinal);
        var englishIndex = body.IndexOf("--- English ---", StringComparison.Ordinal);
        Assert.True(frenchIndex >= 0 && englishIndex > frenchIndex);
    }
}
