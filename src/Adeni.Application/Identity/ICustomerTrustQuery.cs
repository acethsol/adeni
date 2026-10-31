namespace Adeni.Application.Identity;

using Adeni.Domain.Identity;

public sealed record CustomerVerificationState(
    bool EmailVerified,
    bool PhoneVerified,
    DateTimeOffset? EmailVerifiedAt,
    DateTimeOffset? PhoneVerifiedAt);

public interface ICustomerTrustQuery
{
    CustomerVerificationState Evaluate(Customer customer);

    Task<CustomerVerificationState?> GetByAuth0SubAsync(string auth0Sub, CancellationToken cancellationToken = default);
}
