namespace Adeni.Infrastructure.Tests.Finance;

using Adeni.Application.Auth;
using Adeni.Application.Finance;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Finance;
using Adeni.Infrastructure.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class PaymentAccessServiceTests
{
    [Theory]
    [InlineData(null, "member", true, true)]
    [InlineData(null, "other", true, false)]
    [InlineData("customer", null, true, true)]
    [InlineData("customer", "other", true, true)]
    [InlineData("stranger", null, true, false)]
    [InlineData("missing", null, true, false)]
    [InlineData("customer", null, false, false)]
    [InlineData(null, null, true, false)]
    public async Task CanAccessAsync_UsesTenantMembershipOrBookingOwnership(
        string? customerSub, string? businessSub, bool hasBooking, bool expected)
    {
        await using var db = new AdeniDbContext(
            new DbContextOptionsBuilder<AdeniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new TenantContext());
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        db.BusinessUsers.AddRange(
            new BusinessUser { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Auth0Sub = "member" },
            new BusinessUser { Id = Guid.NewGuid(), TenantId = tenantId, Auth0Sub = "member" },
            new BusinessUser { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Auth0Sub = "other" });
        db.Customers.AddRange(
            new Customer { Id = customerId, Auth0Sub = "customer" },
            new Customer { Id = Guid.NewGuid(), Auth0Sub = "stranger" });
        db.Bookings.Add(new BookingRecord { Id = bookingId, TenantId = tenantId, CustomerId = customerId });
        await db.SaveChangesAsync();
        IBusinessMembershipQuery membership = new BusinessMembershipQuery(db);
        ICustomerPaymentAccessQuery customerAccess = new CustomerPaymentAccessQuery(db);
        IPaymentAccessService service = new PaymentAccessService(membership, customerAccess);
        var payment = new PaymentIntentResponse(Guid.NewGuid(), tenantId, hasBooking ? bookingId : null,
            "deposit", 100m, 0m, "NGN", "pending", "", "ref");

        Assert.Equal(expected, await service.CanAccessAsync(payment, customerSub, businessSub));
    }
}
