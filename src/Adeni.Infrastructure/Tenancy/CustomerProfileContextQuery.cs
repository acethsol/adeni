namespace Adeni.Infrastructure.Tenancy;

using Adeni.Application.Subscriptions;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerProfileContextQuery(AdeniDbContext db, IEntitlementsService entitlements) : ICustomerProfileContextQuery
{
    public async Task<Result<CustomerProfileContext>> GetAsync(Guid tenantId, Guid tenantCustomerId, CancellationToken cancellationToken)
    {
        var customer = await db.TenantCustomers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == tenantCustomerId, cancellationToken);
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);
        return customer is null || tenant is null
            ? Result.Failure<CustomerProfileContext>(Error.NotFound("Customer"))
            : Result.Success(new CustomerProfileContext(customer.Id, customer.CustomerId, entitlements.GetEntitlementsForTenant(tenant)));
    }
}
