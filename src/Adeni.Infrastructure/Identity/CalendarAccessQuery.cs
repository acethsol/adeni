namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Calendar;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CalendarAccessQuery(AdeniDbContext db) : ICalendarAccessQuery
{
    public async Task<bool> CanManageAsync(Guid tenantId, string actor, Guid? staffId, CancellationToken ct) =>
        await db.BusinessUsers.AsNoTracking().AnyAsync(x => x.TenantId == tenantId && x.Auth0Sub == actor
            && (x.Role == BusinessPermissionRoles.Owner || x.Role == BusinessPermissionRoles.Manager), ct)
        && (staffId == null || await db.StaffMembers.AsNoTracking().AnyAsync(x => x.TenantId == tenantId && x.Id == staffId && x.IsActive, ct));
}
