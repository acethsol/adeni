namespace Adeni.Infrastructure.Tests.Booking;

using Adeni.Application.Booking;
using Adeni.Application.Notifications;
using Adeni.Domain.Booking;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Tests.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class QueueServiceTests
{
    [Fact]
    public async Task Join_call_start_complete_flow()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<IQueueService>();
        var sink = scope.ServiceProvider.GetRequiredService<NotificationSink>();

        var (tenantId, slug, serviceId) = await SeedBarberAsync(db);

        var joined = await queue.JoinBySlugAsync(
            slug,
            new JoinQueueRequest("Ada Okoro", "+2348011111111", serviceId));
        Assert.True(joined.IsSuccess, joined.Error?.Message);
        Assert.Equal("waiting", joined.Value!.Status);
        Assert.Equal(1, joined.Value.Position);

        var second = await queue.JoinBySlugAsync(
            slug,
            new JoinQueueRequest("Chi", "+2348022222222", serviceId));
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(2, second.Value!.Position);

        var called = await queue.CallAsync(tenantId, joined.Value.Id);
        Assert.True(called.IsSuccess, called.Error?.Message);
        Assert.Equal("called", called.Value!.Status);
        Assert.Contains(sink.Messages, m => m.Subject.Contains("You're up", StringComparison.OrdinalIgnoreCase));

        var started = await queue.StartAsync(tenantId, joined.Value.Id);
        Assert.True(started.IsSuccess, started.Error?.Message);
        Assert.Equal("in_service", started.Value!.Status);
        Assert.NotNull(started.Value.BookingId);

        var completed = await queue.CompleteAsync(tenantId, joined.Value.Id);
        Assert.True(completed.IsSuccess, completed.Error?.Message);
        Assert.Equal("done", completed.Value!.Status);

        var booking = await db.Bookings.SingleAsync(x => x.Id == started.Value.BookingId);
        Assert.Equal(BookingStatus.Completed, booking.Status);
    }

    [Fact]
    public async Task Capability_gate_blocks_non_queue_categories()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<IQueueService>();

        var (_, slug, serviceId) = await SeedBarberAsync(db, categorySlug: "massage-bodywork");
        var joined = await queue.JoinBySlugAsync(
            slug,
            new JoinQueueRequest("Ada", "+2348033333333", serviceId));

        Assert.True(joined.IsFailure);
        Assert.Contains("not enabled", joined.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_show_releases_waiting_guest()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<IQueueService>();
        var (tenantId, slug, serviceId) = await SeedBarberAsync(db);

        var joined = await queue.JoinBySlugAsync(
            slug,
            new JoinQueueRequest("Ada", "+2348044444444", serviceId));
        Assert.True(joined.IsSuccess, joined.Error?.Message);

        var noShow = await queue.MarkNoShowAsync(tenantId, joined.Value!.Id);
        Assert.True(noShow.IsSuccess, noShow.Error?.Message);
        Assert.Equal("no_show", noShow.Value!.Status);
    }

    private static async Task<(Guid TenantId, string Slug, Guid ServiceId)> SeedBarberAsync(
        AdeniDbContext db,
        string categorySlug = "barbers")
    {
        var tenantId = Guid.NewGuid();
        var slug = $"queue-{tenantId:N}"[..20];
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Queue Cut",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessProfiles.Add(new BusinessProfile
        {
            TenantId = tenantId,
            CategorySlug = categorySlug,
            Phone = "+2348000000000",
            Description = "Queue tests",
            BusinessType = BusinessType.ScheduledAppointment,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.BusinessLocations.Add(new BusinessLocation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Slug = slug,
            Name = "Main",
            MarketId = "lagos",
            AddressLine = "1 Cut Rd",
            Area = "Ikeja",
            IsPrimary = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        var serviceId = Guid.NewGuid();
        db.ServiceOfferings.Add(new ServiceOffering
        {
            Id = serviceId,
            TenantId = tenantId,
            Name = "Fade",
            PriceAmount = 5000m,
            Currency = "NGN",
            DurationMinutes = 30,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return (tenantId, slug, serviceId);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCategoryWorkflowCatalog();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddSingleton<NotificationSink>();
        services.AddSingleton<INotificationDispatcher>(sp =>
            new SinkDispatcher(sp.GetRequiredService<NotificationSink>()));
        services.AddScoped<IQueueService, QueueService>();
        return services.BuildServiceProvider();
    }

    private sealed class NotificationSink
    {
        public List<NotificationMessage> Messages { get; } = [];
    }

    private sealed class SinkDispatcher(NotificationSink sink) : INotificationDispatcher
    {
        public Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            sink.Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
