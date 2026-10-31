namespace Adeni.Infrastructure.Identity;

using System.Security.Claims;
using Adeni.Application.Identity;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

public sealed class CustomerVerificationPolicy(
    AdeniDbContext dbContext,
    ICustomerTrustQuery trustQuery,
    IHttpContextAccessor httpContextAccessor,
    IOptions<CustomerVerificationOptions> options) : ICustomerVerificationPolicy
{
    public async Task<Result> RequireEmailVerifiedAsync(
        string auth0Sub,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default)
    {
        user ??= httpContextAccessor.HttpContext?.User;
        if (IsEmailVerifiedFromClaims(user))
        {
            return Result.Success();
        }

        if (options.Value.AutoVerifyCustomerInTesting)
        {
            return Result.Success();
        }

        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Auth0Sub == auth0Sub, cancellationToken);

        if (customer is null)
        {
            return Result.Failure(ErrorCodes.CustomerEmailNotVerifiedError());
        }

        var state = trustQuery.Evaluate(customer);
        return state.EmailVerified
            ? Result.Success()
            : Result.Failure(ErrorCodes.CustomerEmailNotVerifiedError());
    }

    public async Task<Result> RequireEmailAndPhoneVerifiedAsync(
        string auth0Sub,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default)
    {
        user ??= httpContextAccessor.HttpContext?.User;
        var email = await RequireEmailVerifiedAsync(auth0Sub, user, cancellationToken);
        if (email.IsFailure)
        {
            return email;
        }

        if (IsPhoneVerifiedFromClaims(user))
        {
            return Result.Success();
        }

        if (options.Value.AutoVerifyCustomerInTesting)
        {
            return Result.Success();
        }

        var customer = await dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Auth0Sub == auth0Sub, cancellationToken);

        if (customer is null)
        {
            return Result.Failure(ErrorCodes.CustomerPhoneNotVerifiedError());
        }

        var state = trustQuery.Evaluate(customer);
        return state.PhoneVerified
            ? Result.Success()
            : Result.Failure(ErrorCodes.CustomerPhoneNotVerifiedError());
    }

    private static bool IsEmailVerifiedFromClaims(ClaimsPrincipal? user) =>
        user is not null && IsClaimTrue(user, "email_verified");

    private static bool IsPhoneVerifiedFromClaims(ClaimsPrincipal? user) =>
        user is not null && IsClaimTrue(user, "phone_number_verified");

    private static bool IsClaimTrue(ClaimsPrincipal user, string claimType)
    {
        var value = user.FindFirst(claimType)?.Value;
        return bool.TryParse(value, out var parsed) && parsed;
    }
}
