namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Reporting;
using Adeni.Domain.Common;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class DocumentBookingScopeQuery(AdeniDbContext db) : IDocumentBookingScopeQuery
{
    public async Task<Result<Guid>> GetLocationAsync(Guid tenantId, Guid bookingId, CancellationToken ct)
    {
        var location = await db.Bookings.AsNoTracking().Where(x => x.TenantId == tenantId && x.Id == bookingId)
            .Select(x => x.BusinessLocationId).SingleOrDefaultAsync(ct);
        return location is Guid id ? Result.Success(id) : Result.Failure<Guid>(Error.NotFound("Document location"));
    }
}
