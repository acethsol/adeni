namespace Adeni.Api.Tests.Auth;

using System.Security.Claims;
using Adeni.Api.Auth;
using Adeni.Application.Auth;
using Adeni.Domain.Tenancy;

public sealed class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void ToCurrentUser_returns_anonymous_when_unauthenticated()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity()).ToCurrentUser();

        Assert.Null(user.UserId);
        Assert.Empty(user.Roles);
        Assert.Null(user.TenantId);
        Assert.False(user.HasMfa);
    }

    [Fact]
    public void ToCurrentUser_maps_claims()
    {
        var tenant = Guid.NewGuid();
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "auth0|1"),
            new Claim(AdeniClaimTypes.PlatformUserId, "platform-1"),
            new Claim(AdeniClaimTypes.Roles, "admin"),
            new Claim(AdeniClaimTypes.TenantId, tenant.ToString()),
            new Claim(AdeniClaimTypes.Amr, "mfa")
        ],
        authenticationType: "Bearer");

        var user = new ClaimsPrincipal(identity).ToCurrentUser();

        Assert.Equal("platform-1", user.UserId);
        Assert.Contains("admin", user.Roles);
        Assert.Equal(tenant, user.TenantId!.Value.Value);
        Assert.True(user.HasMfa);
    }

    [Fact]
    public void ToCurrentUser_ignores_invalid_tenant_claim()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(AdeniClaimTypes.TenantId, "not-a-guid")
        ],
        authenticationType: "Bearer");

        var user = new ClaimsPrincipal(identity).ToCurrentUser();

        Assert.Null(user.TenantId);
    }
}
