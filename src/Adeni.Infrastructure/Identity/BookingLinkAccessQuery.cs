namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Tenancy;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class BookingLinkAccessQuery(AdeniDbContext db) : IBookingLinkAccessQuery
{
    public Task<bool> CanPublishAsync(Guid tenant, string actor, CancellationToken ct) => db.BusinessUsers.AsNoTracking().AnyAsync(x => x.TenantId == tenant && x.Auth0Sub == actor
        && (x.Role == BusinessPermissionRoles.Owner || x.Role == BusinessPermissionRoles.Manager || x.Role == BusinessPermissionRoles.Ops), ct);
}
