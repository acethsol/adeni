namespace Adeni.Infrastructure.Tenancy;

using Adeni.Application.Identity;
using Adeni.Application.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerProfileTrustQuery(
    AdeniDbContext dbContext,
    ICustomerTrustQuery customerTrust) : ICustomerProfileTrustQuery
{
    public async Task<CustomerProfileTrustIndicators?> GetForPlatformCustomerAsync(
        Guid? platformCustomerId,
        CancellationToken cancellationToken = default)
    {
        if (platformCustomerId is not Guid customerId)
        {
            return null;
        }

        var customer = await dbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var state = customerTrust.Evaluate(customer);
        return new CustomerProfileTrustIndicators(state.PhoneVerified, customer.CreatedAt);
    }
}
