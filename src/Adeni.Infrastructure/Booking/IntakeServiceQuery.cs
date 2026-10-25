namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Intake;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class IntakeServiceQuery(AdeniDbContext db) : IIntakeServiceQuery
{
    public Task<bool> ExistsAsync(Guid tenantId, Guid serviceId, CancellationToken cancellationToken) =>
        db.ServiceOfferings.AsNoTracking().AnyAsync(x => x.TenantId == tenantId && x.Id == serviceId, cancellationToken);
}
