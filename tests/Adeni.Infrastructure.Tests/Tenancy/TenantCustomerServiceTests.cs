namespace Adeni.Infrastructure.Tests.Tenancy;

using Adeni.Infrastructure.Tests.Subscriptions;
using Adeni.Application.Tenancy;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public sealed class TenantCustomerServiceTests
{
    [Fact]
    public async Task UpsertFromWalkIn_creates_then_updates_same_customer()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var customers = scope.ServiceProvider.GetRequiredService<ITenantCustomerService>();
        var tenantId = await SeedTenantAsync(db);

        var first = await customers.UpsertFromWalkInAsync(
            tenantId,
            "Ada Okoro",
            "+2348011111111",
            null);
        Assert.True(first.IsSuccess);
        var tenantCustomerId = first.Value!.TenantCustomerId;

        var second = await customers.UpsertFromWalkInAsync(
            tenantId,
            "Ada O.",
            "+2348011111111",
            "ada@example.com");
        Assert.True(second.IsSuccess);
        Assert.Equal(tenantCustomerId, second.Value!.TenantCustomerId);

        var row = await db.TenantCustomers.SingleAsync(x => x.Id == tenantCustomerId);
        Assert.Equal("Ada O.", row.DisplayName);
        Assert.Equal("ada@example.com", row.Email);
    }

    [Fact]
    public async Task EnsureFromBooking_sets_platform_customer_id_and_last_booking()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var customers = scope.ServiceProvider.GetRequiredService<ITenantCustomerService>();
        var tenantId = await SeedTenantAsync(db);

        var platformCustomerId = Guid.NewGuid();
        db.Customers.Add(new Customer
        {
            Id = platformCustomerId,
            Auth0Sub = "auth0|booker",
            Name = "Booked Ada",
            Phone = "+2348022222222",
            Email = "booked@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await customers.EnsureFromBookingAsync(tenantId, platformCustomerId);
        Assert.True(result.IsSuccess);
        Assert.Equal(platformCustomerId, result.Value!.LinkedPlatformCustomerId);

        var row = await db.TenantCustomers.SingleAsync(x => x.TenantId == tenantId);
        Assert.Equal(platformCustomerId, row.CustomerId);
        Assert.NotNull(row.LastBookingAt);
    }

    [Fact]
    public async Task Walk_in_merges_with_existing_booking_customer_by_phone()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var customers = scope.ServiceProvider.GetRequiredService<ITenantCustomerService>();
        var tenantId = await SeedTenantAsync(db);

        var platformCustomerId = Guid.NewGuid();
        db.Customers.Add(new Customer
        {
            Id = platformCustomerId,
            Auth0Sub = "auth0|same",
            Name = "Same Person",
            Phone = "+2348033333333",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        await customers.EnsureFromBookingAsync(tenantId, platformCustomerId);
        var walkIn = await customers.UpsertFromWalkInAsync(
            tenantId,
            "Same Person",
            "+2348033333333",
            null);

        Assert.True(walkIn.IsSuccess);
        Assert.Equal(1, await db.TenantCustomers.CountAsync(x => x.TenantId == tenantId));
        var row = await db.TenantCustomers.SingleAsync(x => x.TenantId == tenantId);
        Assert.Equal(platformCustomerId, row.CustomerId);
    }

    [Fact]
    public async Task GetById_includes_recent_booking_and_queue_summaries()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var customers = scope.ServiceProvider.GetRequiredService<ITenantCustomerService>();
        var tenantId = await SeedTenantAsync(db);

        var platformCustomerId = Guid.NewGuid();
        db.Customers.Add(new Customer
        {
            Id = platformCustomerId,
            Auth0Sub = "auth0|detail",
            Name = "Detail Guest",
            Phone = "+2348055555555",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var serviceId = Guid.NewGuid();
        db.ServiceOfferings.Add(new ServiceOffering
        {
            Id = serviceId,
            TenantId = tenantId,
            Name = "Fade & lineup",
            PriceAmount = 5000m,
            Currency = "NGN",
            DurationMinutes = 45,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var upsert = await customers.EnsureFromBookingAsync(tenantId, platformCustomerId);
        Assert.True(upsert.IsSuccess);
        var tenantCustomer = await db.TenantCustomers.SingleAsync(x => x.TenantId == tenantId);

        var bookingId = Guid.NewGuid();
        var start = DateTimeOffset.UtcNow.AddDays(-1);
        db.Bookings.Add(new BookingRecord
        {
            Id = bookingId,
            TenantId = tenantId,
            CustomerId = platformCustomerId,
            ServiceOfferingId = serviceId,
            StartAt = start,
            EndAt = start.AddMinutes(45),
            Status = BookingStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var ticketId = Guid.NewGuid();
        db.QueueTickets.Add(new QueueTicket
        {
            Id = ticketId,
            TenantId = tenantId,
            TenantCustomerId = tenantCustomer.Id,
            ServiceOfferingId = serviceId,
            GuestName = tenantCustomer.DisplayName,
            GuestPhone = "+2348055555555",
            Status = QueueTicketStatus.Done,
            JoinedAt = DateTimeOffset.UtcNow.AddHours(-2),
            PublicToken = "tok-detail-test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var detail = await customers.GetByIdAsync(tenantId, tenantCustomer.Id);
        Assert.True(detail.IsSuccess);
        Assert.Single(detail.Value!.RecentBookings);
        Assert.Equal(bookingId, detail.Value.RecentBookings[0].Id);
        Assert.Equal("Fade & lineup", detail.Value.RecentBookings[0].ServiceName);
        Assert.Single(detail.Value.RecentQueueVisits);
        Assert.Equal(ticketId, detail.Value.RecentQueueVisits[0].Id);
        Assert.Equal("Fade & lineup", detail.Value.RecentQueueVisits[0].ServiceName);
    }

    [Fact]
    public async Task Same_phone_two_tenants_two_customer_rows()
    {
        await using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        var customers = scope.ServiceProvider.GetRequiredService<ITenantCustomerService>();
        var tenantA = await SeedTenantAsync(db);
        var tenantB = await SeedTenantAsync(db);

        await customers.UpsertFromWalkInAsync(tenantA, "Ada", "+2348044444444", null);
        await customers.UpsertFromWalkInAsync(tenantB, "Ada", "+2348044444444", null);

        Assert.Equal(2, await db.TenantCustomers.CountAsync());
    }

    private static async Task<Guid> SeedTenantAsync(AdeniDbContext db)
    {
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Customer test",
            Status = TenantStatus.Verified,
            CreatedAt = DateTimeOffset.UtcNow,
            VerifiedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AdeniDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<Adeni.Infrastructure.Context.TenantContext>();
        services.AddScoped<Application.Abstractions.ITenantContext>(sp =>
            sp.GetRequiredService<Adeni.Infrastructure.Context.TenantContext>());
        services.AddEntitlementsModule();
        services.AddScoped<ITenantCustomerService, TenantCustomerService>();
        return services.BuildServiceProvider();
    }
}
