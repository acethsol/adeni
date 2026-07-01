namespace Adeni.Api.Auth;

using System.Security.Claims;
using Adeni.Application.Abstractions;
using Adeni.Application.Auth;
using Adeni.Domain.Tenancy;

public static class ClaimsPrincipalExtensions
{
    public static ICurrentUser ToCurrentUser(this ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return new HttpCurrentUser(null, [], null, false);
        }

        var userId = principal.FindFirstValue(AdeniClaimTypes.PlatformUserId)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        var roles = principal.FindAll(AdeniClaimTypes.Roles)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        TenantId? tenantId = null;
        var tenantClaim = principal.FindFirstValue(AdeniClaimTypes.TenantId);
        if (Guid.TryParse(tenantClaim, out var tenantGuid))
        {
            var parsed = TenantId.Create(tenantGuid);
            if (parsed.IsSuccess)
            {
                tenantId = parsed.Value;
            }
        }

        var hasMfa = principal.FindAll(AdeniClaimTypes.Amr)
            .Any(c => c.Value.Equals("mfa", StringComparison.OrdinalIgnoreCase));

        return new HttpCurrentUser(userId, roles, tenantId, hasMfa);
    }
}

public sealed record HttpCurrentUser(
    string? UserId,
    IReadOnlyCollection<string> Roles,
    TenantId? TenantId,
    bool HasMfa) : ICurrentUser;
