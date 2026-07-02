namespace Adeni.Api.Tests.Controllers;

using Adeni.Api.Controllers;
using Adeni.Application.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

public sealed class AuthControllerMeTests
{
    [Fact]
    public void Me_returns_not_implemented_when_auth0_disabled()
    {
        var controller = new AuthController(
            new StubAuthSyncService(),
            Options.Create(new Auth0Options { Enabled = false }));

        var result = controller.Me();

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(501, status.StatusCode);
    }

    private sealed class StubAuthSyncService : IAuthSyncService
    {
        public Task<Domain.Common.Result<UserProfileResponse>> SyncAsync(
            SyncAuthUserRequest request,
            string? authenticatedAuth0Sub,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
