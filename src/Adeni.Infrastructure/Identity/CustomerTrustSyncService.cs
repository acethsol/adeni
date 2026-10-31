namespace Adeni.Infrastructure.Identity;

using System.Security.Claims;
using Adeni.Application.Identity;
using Adeni.Application.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class CustomerTrustSyncService(
    AdeniDbContext dbContext,
    ICustomerPhoneFingerprintService fingerprints,
    IOptions<CustomerVerificationOptions> options,
    TimeProvider clock) : ICustomerTrustSyncService
{
    public async Task SyncAsync(string auth0Sub, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return;
        }

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Auth0Sub == auth0Sub, cancellationToken);

        if (customer is null)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var changed = false;

        if (user is not null)
        {
            var email = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value;
            if (!string.IsNullOrWhiteSpace(email))
            {
                customer.Email = email.Trim();
                changed = true;
            }

            var phone = user.FindFirst("phone_number")?.Value;
            if (!string.IsNullOrWhiteSpace(phone))
            {
                var normalized = PhoneNormalizer.Normalize(phone);
                if (normalized is not null && !string.Equals(customer.Phone, normalized, StringComparison.Ordinal))
                {
                    customer.Phone = normalized;
                    changed = true;
                }
            }

            if (IsClaimTrue(user, "email_verified"))
            {
                customer.EmailVerifiedAt ??= now;
                changed = true;
            }

            if (IsClaimTrue(user, "phone_number_verified"))
            {
                var normalizedPhone = PhoneNormalizer.Normalize(customer.Phone);
                var fingerprint = fingerprints.ComputeFingerprint(normalizedPhone);
                if (fingerprint is not null)
                {
                    customer.PhoneVerifiedAt = now;
                    customer.PhoneVerifiedFingerprint = fingerprint;
                    changed = true;
                }
            }
        }

        if (options.Value.AutoVerifyCustomerInTesting)
        {
            customer.EmailVerifiedAt ??= now;
            var normalized = PhoneNormalizer.Normalize(customer.Phone) ?? "+15555550100";
            customer.Phone ??= normalized;
            var fingerprint = fingerprints.ComputeFingerprint(normalized);
            if (fingerprint is not null)
            {
                customer.PhoneVerifiedAt ??= now;
                customer.PhoneVerifiedFingerprint ??= fingerprint;
            }

            changed = true;
        }

        if (changed)
        {
            customer.TrustUpdatedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool IsClaimTrue(ClaimsPrincipal user, string claimType)
    {
        var value = user.FindFirst(claimType)?.Value;
        return bool.TryParse(value, out var parsed) && parsed;
    }
}
