namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Intake;
using Adeni.Domain.Identity;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class IntakeManagementAccessQuery(AdeniDbContext db) : IIntakeManagementAccessQuery
{
    public Task<bool> CanManageAsync(Guid tenantId, string auth0Sub, CancellationToken cancellationToken) =>
        db.BusinessUsers.AsNoTracking().AnyAsync(x => x.TenantId == tenantId && x.Auth0Sub == auth0Sub
            && (x.Role == BusinessPermissionRoles.Owner || x.Role == BusinessPermissionRoles.Manager), cancellationToken);
}
