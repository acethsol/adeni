namespace Adeni.Infrastructure.Finance;

using Adeni.Application.Auth;
using Adeni.Application.Finance;

public sealed class PaymentAccessService(
    IBusinessMembershipQuery businessMembership,
    ICustomerPaymentAccessQuery customerAccess) : IPaymentAccessService
{
    public async Task<bool> CanAccessAsync(
        PaymentIntentResponse payment,
        string? customerAuth0Sub,
        string? businessAuth0Sub,
        CancellationToken cancellationToken = default)
    {
        if (businessAuth0Sub is not null
            && await businessMembership.IsMemberAsync(businessAuth0Sub, payment.TenantId, cancellationToken))
        {
            return true;
        }

        return customerAuth0Sub is not null && payment.BookingId is Guid bookingId
            && await customerAccess.OwnsBookingAsync(customerAuth0Sub, bookingId, cancellationToken);
    }
}
