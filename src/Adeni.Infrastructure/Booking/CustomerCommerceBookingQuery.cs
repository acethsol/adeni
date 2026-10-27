namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Finance;
using Adeni.Application.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerCommerceBookingQuery(AdeniDbContext db) : ICustomerCommerceBookingQuery
{
    public async Task<IReadOnlyList<Guid>> GetBookingIdsAsync(CustomerProfileScope scope, CancellationToken cancellationToken) =>
        await db.Bookings.AsNoTracking()
            .Where(x => x.TenantId == scope.TenantId && scope.CustomerId != null && x.CustomerId == scope.CustomerId
                && (scope.LocationId == null || x.BusinessLocationId == scope.LocationId))
            .Select(x => x.Id).ToArrayAsync(cancellationToken);
}
