namespace Adeni.Application.Identity;

using Adeni.Domain.Common;
using System.Security.Claims;

public interface ICustomerVerificationPolicy
{
    Task<Result> RequireEmailVerifiedAsync(string auth0Sub, ClaimsPrincipal? user, CancellationToken cancellationToken = default);

    Task<Result> RequireEmailAndPhoneVerifiedAsync(string auth0Sub, ClaimsPrincipal? user, CancellationToken cancellationToken = default);
}
