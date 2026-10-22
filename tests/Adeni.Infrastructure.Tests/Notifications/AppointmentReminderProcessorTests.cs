namespace Adeni.Infrastructure.Tests.Notifications;

using Adeni.Domain.Booking;
using Adeni.Domain.Subscriptions;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Notifications;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public sealed class AppointmentReminderProcessorTests
{
    [Fact]
    public async Task Reminder_job_idempotent_for_same_booking_window()
    {
        await using var provider = NotificationDispatcherTests.BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var startAt = DateTimeOffset.UtcNow.AddHours(24);

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Reminders",
            SubscriptionSku = SubscriptionSku.Professional,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.Customers.Add(new Domain.Identity.Customer
        {
            Id = customerId,
            Email = "reminder@example.com",
            Name = "Guest",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.Bookings.Add(new BookingRecord
        {
            Id = bookingId,
            TenantId = tenantId,
            CustomerId = customerId,
            ServiceOfferingId = Guid.NewGuid(),
            StartAt = startAt,
            EndAt = startAt.AddHours(1),
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var processor = new AppointmentReminderProcessor(
            db,
            scope.ServiceProvider.GetRequiredService<Application.Notifications.INotificationDispatcher>(),
            scope.ServiceProvider.GetRequiredService<Application.Notifications.INotificationRecipientResolver>(),
            scope.ServiceProvider.GetRequiredService<Application.Subscriptions.IEntitlementsService>(),
            Options.Create(new NotificationsOptions()));

        await processor.ProcessDueRemindersAsync(CancellationToken.None);
        await processor.ProcessDueRemindersAsync(CancellationToken.None);

        var sink = scope.ServiceProvider.GetRequiredService<NotificationDispatcherTests.CapturingEmailProvider>();
        Assert.Single(sink.Sent);
        Assert.Equal(1, await db.NotificationDeliveries.CountAsync());
    }
}
