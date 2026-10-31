namespace Adeni.Application.Tenancy;

using Adeni.Application.Identity;

public interface ICustomerProfileTrustQuery
{
    Task<CustomerProfileTrustIndicators?> GetForPlatformCustomerAsync(
        Guid? platformCustomerId,
        CancellationToken cancellationToken = default);
}
