namespace Adeni.Application.Booking;

using Adeni.Application.Tenancy;

public interface ICustomerBookingActivityQuery
{
    Task<CustomerBookingProfile> GetProfileAsync(CustomerProfileScope scope, DateTimeOffset asOf, bool includeAnalytics, CancellationToken cancellationToken);
    Task<CustomerActivityBatch> GetActivityAsync(CustomerProfileScope scope, DateTimeOffset from, DateTimeOffset asOf,
        int bookingLimit, CancellationToken cancellationToken);
}
