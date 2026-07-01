namespace Adeni.Infrastructure.Tests.Auth;

using Adeni.Application.Auth;
using Adeni.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;

public sealed class AdminMfaAuthorizationHandlerTests
{
    [Fact]
    public async Task Succeeds_when_mfa_not_required()
    {
        var handler = new AdminMfaAuthorizationHandler(Options.Create(new Auth0Options { RequireMfaForAdmin = false }));
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AdeniClaimTypes.Roles, AdeniRoles.Admin)], "Bearer"));
        var context = new AuthorizationHandlerContext([new AdminMfaRequirement()], user, null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Succeeds_when_amr_contains_mfa()
    {
        var handler = new AdminMfaAuthorizationHandler(Options.Create(new Auth0Options { RequireMfaForAdmin = true }));
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(AdeniClaimTypes.Roles, AdeniRoles.Admin),
            new Claim(AdeniClaimTypes.Amr, "mfa")
        ],
        "Bearer"));
        var context = new AuthorizationHandlerContext([new AdminMfaRequirement()], user, null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_when_admin_without_mfa()
    {
        var handler = new AdminMfaAuthorizationHandler(Options.Create(new Auth0Options { RequireMfaForAdmin = true }));
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AdeniClaimTypes.Roles, AdeniRoles.Admin)], "Bearer"));
        var context = new AuthorizationHandlerContext([new AdminMfaRequirement()], user, null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
