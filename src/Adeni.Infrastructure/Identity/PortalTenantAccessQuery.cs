namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Auth;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class PortalTenantAccessQuery(AdeniDbContext db) : IPortalTenantAccessQuery, ICustomerErasureQuery
{
    public async Task<Result<PortalAccessSnapshot>> ResolveAsync(Guid tenantId, string auth0Sub, CancellationToken ct)
    {
        var user = await db.BusinessUsers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Auth0Sub == auth0Sub, ct);
        if (string.IsNullOrWhiteSpace(auth0Sub) || user is null || !PortalPermissionRoles.IsValid(user.Role))
            return Result.Failure<PortalAccessSnapshot>(ErrorCodes.PermissionDeniedError());
        var role = user.Role.Trim().ToLowerInvariant();
        return Result.Success(new PortalAccessSnapshot(user.Id, tenantId, role, PortalPermissionRoles.PermissionsFor(role), user.StaffMemberId));
    }

    public async Task<IReadOnlySet<Guid>> GetErasedAsync(IReadOnlyCollection<Guid> customerIds, CancellationToken ct)
    {
        var active = await db.Customers.AsNoTracking().Where(x => customerIds.Contains(x.Id) && x.ErasureRequestedAt == null)
            .Select(x => x.Id).ToArrayAsync(ct);
        // A missing identity must not regenerate inferred data after a later physical privacy deletion.
        return customerIds.Except(active).ToHashSet();
    }
}
