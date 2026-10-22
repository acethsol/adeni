namespace Adeni.Infrastructure.Tests.Notifications;

using Adeni.Application.Abstractions;
using Adeni.Application.Notifications;
using Adeni.Domain.Booking;
using Adeni.Domain.Booking.Events;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using TenantId = Adeni.Domain.Tenancy.TenantId;
using Adeni.Infrastructure.Notifications;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class BookingNotificationHandlerTests
{
    [Fact]
    public async Task BookingConfirmed_sends_email_to_resolved_customer_address()
    {
        var dispatcher = new CapturingDispatcher();
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Shop", CreatedAt = DateTimeOffset.UtcNow });
        db.Customers.Add(new Customer
        {
            Id = customerId,
            Auth0Sub = "auth0|1",
            Name = "Guest",
            Email = "guest@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
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

        var handler = new BookingNotificationHandler(
            dispatcher,
            new NotificationRecipientResolver(db),
            NullLogger<BookingNotificationHandler>.Instance);

        await handler.HandleAsync(
            new BookingConfirmed(bookingId, tenantId, customerId, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow));

        Assert.Single(dispatcher.Messages);
        Assert.Equal("guest@example.com", dispatcher.Messages[0].RecipientKey);
        Assert.Equal("Booking confirmed", dispatcher.Messages[0].Subject);
        Assert.Equal($"booking-confirmed:{bookingId:D}", dispatcher.Messages[0].DedupeEventId);
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

    private sealed class CapturingDispatcher : INotificationDispatcher
    {
        public List<NotificationMessage> Messages { get; } = [];

        public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
