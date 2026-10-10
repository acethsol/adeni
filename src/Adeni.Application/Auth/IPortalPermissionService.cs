namespace Adeni.Application.Auth;

using Adeni.Domain.Common;

public sealed record PortalAccessSnapshot(
    Guid BusinessUserId,
    Guid TenantId,
    string PermissionRole,
    IReadOnlyList<string> Permissions,
    Guid? StaffMemberId);

public interface IPortalPermissionService
{
    Task<Result<PortalAccessSnapshot>> ResolveAsync(
        string auth0Sub,
        CancellationToken cancellationToken = default);

    Task<Result> EnsureAsync(
        string auth0Sub,
        string permission,
        CancellationToken cancellationToken = default);
}
