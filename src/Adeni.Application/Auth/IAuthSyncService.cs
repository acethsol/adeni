namespace Adeni.Application.Auth;

using Adeni.Domain.Common;

public sealed record SyncAuthUserRequest(
    string Auth0Sub,
    string? Name,
    string? Email,
    string? Phone,
    string RoleHint);

public sealed record UserProfileResponse(
    Guid PlatformUserId,
    string Auth0Sub,
    string Role,
    string? Name,
    string? Email,
    Guid? TenantId);

public interface IAuthSyncService
{
    Task<Result<UserProfileResponse>> SyncAsync(
        SyncAuthUserRequest request,
        string? authenticatedAuth0Sub,
        CancellationToken cancellationToken = default);
}
