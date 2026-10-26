namespace Adeni.Infrastructure.Calendar;

using Adeni.Application.Calendar;
using Adeni.Domain.Calendar;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class ExternalCalendarAvailability(AdeniDbContext db, TimeProvider clock) : IExternalCalendarAvailability
{
    public bool IsAdmissionConflict(Exception error) => error is DbUpdateConcurrencyException conflict
        && conflict.Entries.Any(x => x.Entity is CalendarStaffState);
    public async Task<IReadOnlyList<CalendarStaffAvailability>> ReadAsync(Guid tenantId, IReadOnlyList<Guid> staffIds,
        DateTimeOffset start, DateTimeOffset end, bool stageAdmission, CancellationToken ct)
    {
        var connections = await db.Set<CalendarConnection>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && staffIds.Contains(x.StaffMemberId) && x.Status != "disconnected"
                && db.Set<CalendarSelection>().Any(s => s.TenantId == tenantId && s.ConnectionId == x.Id && !s.IsWriteTarget)).ToArrayAsync(ct);
        if (connections.Length == 0) return [];
        var connectedIds = connections.Select(x => x.StaffMemberId).Distinct().ToArray();
        // Read before snapshots. Publication/disconnect must modify this same row; the booking save then detects a race.
        var guards = await db.Set<CalendarStaffState>().Where(x => x.TenantId == tenantId && connectedIds.Contains(x.Id)).ToArrayAsync(ct);
        var ids = connections.Select(x => x.Id).ToArray();
        var snapshots = await db.Set<CalendarSnapshot>().AsNoTracking().Where(x => x.TenantId == tenantId
            && ids.Contains(x.ConnectionId) && x.StartAt <= start && x.EndAt >= end).ToArrayAsync(ct);
        var fresh = snapshots.Where(x => x.SyncedAt >= clock.GetUtcNow().AddMinutes(-15)).ToArray();
        var snapshotIds = fresh.Select(x => x.Id).ToArray();
        var blocks = await db.Set<ExternalBusyBlock>().AsNoTracking().Where(x => x.TenantId == tenantId
            && snapshotIds.Contains(x.SnapshotId) && x.StartAt < end && x.EndAt > start).ToArrayAsync(ct);
        var result = connectedIds.Select(staff =>
        {
            var required = connections.Where(x => x.StaffMemberId == staff).Select(x => x.Id).ToArray();
            var complete = guards.Any(x => x.Id == staff) && required.All(id => fresh.Any(s => s.ConnectionId == id));
            var chosen = required.Select(id => fresh.Where(s => s.ConnectionId == id).OrderByDescending(s => s.SyncedAt).FirstOrDefault())
                .Where(x => x != null).Select(x => x!.Id).ToHashSet();
            return new CalendarStaffAvailability(staff, complete, blocks.Where(x => chosen.Contains(x.SnapshotId))
                .Select(x => new CalendarBusyInterval(x.StartAt, x.EndAt)).ToArray());
        }).ToArray();
        // Expansion requests are snapshots with no successful sync. No provider I/O in scheduling.
        if (end > start && end - start <= TimeSpan.FromDays(14))
        {
            foreach (var connection in connections.Where(c => !snapshots.Any(s => s.ConnectionId == c.Id)))
            {
                if (await db.Set<CalendarSnapshot>().CountAsync(x => x.TenantId == tenantId && x.ConnectionId == connection.Id, ct) >= 16) continue;
                db.Add(new CalendarSnapshot { Id = Guid.NewGuid(), TenantId = tenantId, ConnectionId = connection.Id,
                    StartAt = start.ToUniversalTime(), EndAt = end.ToUniversalTime(), SyncedAt = DateTimeOffset.MinValue });
            }
            // Save only pending coverage requests before staging guards for an admitted booking.
            if (db.ChangeTracker.Entries<CalendarSnapshot>().Any(x => x.State == EntityState.Added))
            {
                foreach (var guard in guards) db.Entry(guard).State = EntityState.Unchanged;
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException) { foreach (var entry in db.ChangeTracker.Entries<CalendarSnapshot>().Where(x => x.State == EntityState.Added).ToArray()) entry.State = EntityState.Detached; }
            }
        }
        if (stageAdmission) foreach (var guard in guards) guard.AdmissionStamp = Guid.NewGuid();
        return result;
    }
}
