namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerPaymentAccessQuery(AdeniDbContext dbContext) : ICustomerPaymentAccessQuery
{
    public async Task<bool> OwnsBookingAsync(string auth0Sub, Guid bookingId, CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Auth0Sub == auth0Sub, cancellationToken);
        if (customer is null)
        {
            return false;
        }

        return await dbContext.Bookings.AsNoTracking()
            .AnyAsync(b => b.Id == bookingId && b.CustomerId == customer.Id, cancellationToken);
    }
}
