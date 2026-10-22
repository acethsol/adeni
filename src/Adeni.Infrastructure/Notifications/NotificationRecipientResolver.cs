namespace Adeni.Infrastructure.Notifications;

using Adeni.Application.Notifications;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class NotificationRecipientResolver(AdeniDbContext dbContext) : INotificationRecipientResolver
{
    public async Task<Result<NotificationContact>> ResolvePlatformCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);

        if (customer is null)
        {
            return Result.Failure<NotificationContact>(Error.NotFound("Customer"));
        }

        return Result.Success(new NotificationContact(customer.Email, customer.Phone));
    }

    public async Task<Result<NotificationContact>> ResolveBookingCustomerAsync(
        Guid tenantId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        var booking = await dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.TenantId == tenantId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure<NotificationContact>(Error.NotFound("Booking"));
        }

        return await ResolvePlatformCustomerAsync(booking.CustomerId, cancellationToken);
    }
}
