namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Intake;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class IntakeBookingQuery(AdeniDbContext db) : IIntakeBookingQuery
{
    public Task<IntakeBookingFacts?> GetAsync(Guid tenantId, Guid bookingId, CancellationToken ct) => db.Bookings.AsNoTracking()
        .Where(x => x.TenantId == tenantId && x.Id == bookingId)
        .Select(x => new IntakeBookingFacts(x.Id, x.TenantId, x.CustomerId, x.StaffMemberId, x.BusinessLocationId, x.StartAt, x.EndAt, (int)x.Status, x.UpdatedAt))
        .SingleOrDefaultAsync(ct);

    public async Task<IntakeBookingFacts?> GetOwnedAsync(string customerSub, Guid bookingId, CancellationToken ct)
    {
        var row = await db.Bookings.AsNoTracking().Where(x => x.Id == bookingId
            && db.Customers.Any(c => c.Id == x.CustomerId && c.Auth0Sub == customerSub && c.ErasureRequestedAt == null))
            .Select(x => new { x.TenantId }).SingleOrDefaultAsync(ct);
        return row is null ? null : await GetAsync(row.TenantId, bookingId, ct);
    }

    public async Task<IReadOnlyList<Guid>> ListForCustomerAsync(Guid customerId, CancellationToken ct) =>
        await db.Bookings.AsNoTracking().Where(x => x.CustomerId == customerId).Select(x => x.Id).ToArrayAsync(ct);
}
