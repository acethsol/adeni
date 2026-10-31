namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Identity;
using Adeni.Application.Tenancy;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerTrustQuery(
    AdeniDbContext dbContext,
    ICustomerPhoneFingerprintService fingerprints) : ICustomerTrustQuery
{
    public CustomerVerificationState Evaluate(Customer customer)
    {
        var phone = PhoneNormalizer.Normalize(customer.Phone);
        var phoneVerified = customer.PhoneVerifiedAt is not null
            && fingerprints.PhoneMatchesFingerprint(phone, customer.PhoneVerifiedFingerprint);

        return new CustomerVerificationState(
            customer.EmailVerifiedAt is not null,
            phoneVerified,
            customer.EmailVerifiedAt,
            phoneVerified ? customer.PhoneVerifiedAt : null);
    }

    public async Task<CustomerVerificationState?> GetByAuth0SubAsync(
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Auth0Sub == auth0Sub, cancellationToken);

        return customer is null ? null : Evaluate(customer);
    }
}
