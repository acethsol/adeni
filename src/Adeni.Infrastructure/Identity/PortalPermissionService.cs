namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Auth;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class PortalPermissionService(AdeniDbContext dbContext) : IPortalPermissionService
{
    public async Task<Result<PortalAccessSnapshot>> ResolveAsync(
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Result.Failure<PortalAccessSnapshot>(ErrorCodes.AuthRequiredError());
        }

        var user = await dbContext.BusinessUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Auth0Sub == auth0Sub, cancellationToken);

        if (user is null)
        {
            return Result.Failure<PortalAccessSnapshot>(ErrorCodes.BusinessAccessDeniedError());
        }

        var role = PortalPermissionRoles.Normalize(user.Role);
        return Result.Success(new PortalAccessSnapshot(
            user.Id,
            user.TenantId,
            role,
            PortalPermissionRoles.PermissionsFor(role),
            user.StaffMemberId));
    }

    public async Task<Result> EnsureAsync(
        string auth0Sub,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(auth0Sub, cancellationToken);
        if (!resolved.IsSuccess || resolved.Value is null)
        {
            return Result.Failure(resolved.Error);
        }

        if (!PortalPermissionRoles.Has(resolved.Value.PermissionRole, permission))
        {
            return Result.Failure(ErrorCodes.PermissionDeniedError());
        }

        return Result.Success();
    }
}
