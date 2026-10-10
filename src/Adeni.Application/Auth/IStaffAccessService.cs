namespace Adeni.Application.Auth;

using Adeni.Domain.Common;

public sealed record CreateStaffInviteRequest(
    string Email,
    string? PermissionRole = null);

public sealed record AcceptStaffInviteRequest(string Token);

public sealed record StaffPortalInviteResponse(
    Guid InviteId,
    string Email,
    string PermissionRole,
    Guid? StaffMemberId,
    string Status,
    DateTimeOffset ExpiresAt);

public sealed record AcceptStaffInviteResponse(
    Guid PlatformUserId,
    string Auth0Sub,
    Guid TenantId,
    string PermissionRole,
    Guid? StaffMemberId);

public interface IStaffAccessService
{
    Task<Result<StaffPortalInviteResponse>> InviteStaffMemberAsync(
        Guid tenantId,
        Guid staffMemberId,
        CreateStaffInviteRequest request,
        string invitedByAuth0Sub,
        CancellationToken cancellationToken = default);

    Task<Result<StaffPortalInviteResponse>> InviteAccessOnlyAsync(
        Guid tenantId,
        CreateStaffInviteRequest request,
        string invitedByAuth0Sub,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StaffPortalInviteResponse>> ListInvitesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Result<StaffPortalInviteResponse>> ResendAsync(
        Guid tenantId,
        Guid inviteId,
        CancellationToken cancellationToken = default);

    Task<Result> RevokeAsync(
        Guid tenantId,
        Guid inviteId,
        CancellationToken cancellationToken = default);

    Task<Result<AcceptStaffInviteResponse>> AcceptAsync(
        string auth0Sub,
        string? authenticatedEmail,
        AcceptStaffInviteRequest request,
        CancellationToken cancellationToken = default);
}
