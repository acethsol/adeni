namespace Adeni.Infrastructure.Finance;

using Adeni.Application.Auth;
using Adeni.Application.Finance;
using Adeni.Domain.Identity;

public sealed class PaymentAccessService(
    IBusinessPermissionService businessPermissions,
    ICustomerPaymentAccessQuery customerAccess) : IPaymentAccessService
{
    public async Task<bool> CanAccessAsync(
        PaymentIntentResponse payment,
        string? customerAuth0Sub,
        string? businessAuth0Sub,
        CancellationToken cancellationToken = default)
    {
        if (businessAuth0Sub is not null)
        {
            var resolved = await businessPermissions.ResolveAsync(businessAuth0Sub, cancellationToken);
            if (resolved.IsSuccess
                && resolved.Value is not null
                && resolved.Value.TenantId == payment.TenantId
                && BusinessPermissionRoles.HasAny(
                    resolved.Value.PermissionRole,
                    BusinessPermissions.Finance,
                    BusinessPermissions.Bookings))
            {
                return true;
            }
        }

        return customerAuth0Sub is not null && payment.BookingId is Guid bookingId
            && await customerAccess.OwnsBookingAsync(customerAuth0Sub, bookingId, cancellationToken);
    }
}
