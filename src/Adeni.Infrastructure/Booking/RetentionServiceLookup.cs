namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>Booking-owned read of service offerings for Retention (no Booking DbSets inside Retention).</summary>
public sealed class RetentionServiceLookup(AdeniDbContext db) : IRetentionServiceLookup
{
    public async Task<IReadOnlyList<ServiceOfferingLookup>> FindAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> serviceOfferingIds,
        CancellationToken cancellationToken = default) =>
        await db.ServiceOfferings.AsNoTracking()
            .Where(x => x.TenantId == tenantId && serviceOfferingIds.Contains(x.Id))
            .Select(x => new ServiceOfferingLookup(x.Id, x.Name, x.IsActive))
            .ToListAsync(cancellationToken);
}