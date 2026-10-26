namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class BookingPreselectionQuery(AdeniDbContext db) : IBookingPreselectionQuery
{
    public async Task<bool> IsValidAsync(Guid tenant, Guid location, Guid? service, Guid? practitioner, CancellationToken ct)
    {
        if (service != null && !await db.ServiceOfferings.AsNoTracking().AnyAsync(x => x.TenantId == tenant && x.Id == service && x.IsActive && !x.IsAddOn, ct)) return false;
        if (practitioner == null) return true;
        return await db.StaffMembers.AsNoTracking().AnyAsync(x => x.TenantId == tenant && x.Id == practitioner && x.IsActive
            && (x.BusinessLocationId == null || x.BusinessLocationId == location)
            && (service == null || !x.ServiceLinks.Any() || x.ServiceLinks.Any(s => s.ServiceOfferingId == service)), ct);
    }
}
