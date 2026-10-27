namespace Adeni.Infrastructure.Identity;

using Adeni.Application.Auth;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class BusinessMembershipQuery(AdeniDbContext dbContext) : IBusinessMembershipQuery
{
    public Task<bool> IsMemberAsync(string auth0Sub, Guid tenantId, CancellationToken cancellationToken = default) =>
        dbContext.BusinessUsers.AsNoTracking()
            .AnyAsync(b => b.Auth0Sub == auth0Sub && b.TenantId == tenantId, cancellationToken);
}
