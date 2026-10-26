namespace Adeni.Infrastructure.Tenancy;

using Adeni.Application.Subscriptions;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerSegmentContextQuery(AdeniDbContext db, IEntitlementsService entitlements) : ICustomerSegmentContextQuery
{
    public async Task<Result<CustomerSegmentContext>> GetAsync(Guid tenantId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tenantId, ct);
        var customers = await db.TenantCustomers.AsNoTracking().Where(x => x.TenantId == tenantId && ids.Contains(x.Id))
            .Select(x => new CustomerProfileScope(tenantId, x.Id, x.CustomerId, null)).ToArrayAsync(ct);
        return tenant is null || customers.Length != ids.Count ? Result.Failure<CustomerSegmentContext>(Error.NotFound("Customer"))
            : Result.Success(new CustomerSegmentContext(customers, entitlements.GetEntitlementsForTenant(tenant)));
    }

    public async Task<IReadOnlyList<(Guid TenantId, Guid TenantCustomerId)>> FindLinkedAsync(Guid customerId, CancellationToken ct)
    {
        var rows = await db.TenantCustomers.AsNoTracking().Where(x => x.CustomerId == customerId)
            .Select(x => new { x.TenantId, x.Id }).ToArrayAsync(ct);
        return rows.Select(x => (x.TenantId, x.Id)).ToArray();
    }

}
