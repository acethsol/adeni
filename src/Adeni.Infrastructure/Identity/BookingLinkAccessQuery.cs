namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Tenancy;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class BookingLinkAccessQuery(AdeniDbContext db) : IBookingLinkAccessQuery
{
    public Task<bool> CanPublishAsync(Guid tenant, string actor, CancellationToken ct) => db.BusinessUsers.AsNoTracking().AnyAsync(x => x.TenantId == tenant && x.Auth0Sub == actor
        && (x.Role == PortalPermissionRoles.Owner || x.Role == PortalPermissionRoles.Manager || x.Role == PortalPermissionRoles.Ops), ct);
}
