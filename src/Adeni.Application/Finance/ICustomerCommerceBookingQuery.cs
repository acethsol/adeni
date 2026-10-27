namespace Adeni.Application.Finance;

using Adeni.Application.Tenancy;

/// <summary>Booking-owned identity and location evidence for Finance's customer read model.</summary>
public interface ICustomerCommerceBookingQuery
{
    Task<IReadOnlyList<Guid>> GetBookingIdsAsync(CustomerProfileScope scope, CancellationToken cancellationToken);
}
