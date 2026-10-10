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
            new StubStaffAccessService(),
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

    private sealed class StubStaffAccessService : IStaffAccessService
    {
        public Task<Domain.Common.Result<StaffPortalInviteResponse>> InviteStaffMemberAsync(
            Guid tenantId,
            Guid staffMemberId,
            CreateStaffInviteRequest request,
            string invitedByAuth0Sub,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Domain.Common.Result<StaffPortalInviteResponse>> InviteAccessOnlyAsync(
            Guid tenantId,
            CreateStaffInviteRequest request,
            string invitedByAuth0Sub,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<StaffPortalInviteResponse>> ListInvitesAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Domain.Common.Result<StaffPortalInviteResponse>> ResendAsync(
            Guid tenantId,
            Guid inviteId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Domain.Common.Result> RevokeAsync(
            Guid tenantId,
            Guid inviteId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Domain.Common.Result<AcceptStaffInviteResponse>> AcceptAsync(
            string auth0Sub,
            string? authenticatedEmail,
            AcceptStaffInviteRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<TenantAccessUserResponse>> ListUsersAsync(
            Guid tenantId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Domain.Common.Result<TenantAccessUserResponse>> UpdateRoleAsync(
            Guid tenantId,
            Guid businessUserId,
            UpdateAccessUserRequest request,
            string actorAuth0Sub,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Domain.Common.Result> RevokeLoginAsync(
            Guid tenantId,
            Guid businessUserId,
            string actorAuth0Sub,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
