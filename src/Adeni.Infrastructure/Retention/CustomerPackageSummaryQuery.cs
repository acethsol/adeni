namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Retention;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerPackageSummaryQuery(AdeniDbContext db, TimeProvider clock) : ICustomerPackageSummaryQuery
{
    public async Task<CustomerPackageSummary?> GetSummaryAsync(
        Guid tenantId, Guid tenantCustomerId, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var active = await db.Set<CustomerPackage>().AsNoTracking()
            .Include(x => x.Balances)
            .Where(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId
                && x.Status == CustomerPackageStatus.Active && (x.ExpiresAt == null || x.ExpiresAt > now))
            .OrderBy(x => x.ExpiresAt)
            .ToListAsync(cancellationToken);

        return new CustomerPackageSummary(
            active.Count,
            active.Sum(x => x.Balances.Sum(b => b.Remaining)),
            active.Select(x => x.NameSnapshot).Take(5).ToArray());
    }
}