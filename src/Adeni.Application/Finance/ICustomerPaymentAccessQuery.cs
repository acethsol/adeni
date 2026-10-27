namespace Adeni.Application.Finance;

/// <summary>Booking ownership evidence needed by Finance to authorize customer payment reads.</summary>
public interface ICustomerPaymentAccessQuery
{
    Task<bool> OwnsBookingAsync(string auth0Sub, Guid bookingId, CancellationToken cancellationToken = default);
}
