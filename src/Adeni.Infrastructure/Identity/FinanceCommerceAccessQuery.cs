namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Finance;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class FinanceCommerceAccessQuery(AdeniDbContext db) : IFinanceCommerceAccessQuery
{
    public Task<bool> CanManageAsync(Guid tenantId, string actor, CancellationToken ct) =>
        db.BusinessUsers.AsNoTracking().AnyAsync(x => x.TenantId == tenantId && x.Auth0Sub == actor
            && (x.Role == PortalPermissionRoles.Owner || x.Role == PortalPermissionRoles.Manager), ct);
    public Task<bool> CanReadAsync(Guid tenantId, string actor, CancellationToken ct) =>
        db.BusinessUsers.AsNoTracking().AnyAsync(x => x.TenantId == tenantId && x.Auth0Sub == actor
            && (x.Role == PortalPermissionRoles.Owner || x.Role == PortalPermissionRoles.Manager || x.Role == PortalPermissionRoles.Accountant), ct);
    public Task<Guid?> CustomerIdAsync(string actor, CancellationToken ct) => db.Customers.AsNoTracking()
        .Where(x => x.Auth0Sub == actor && x.ErasureRequestedAt == null).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
}
