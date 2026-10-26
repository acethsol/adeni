namespace Adeni.Infrastructure.Calendar;

using Adeni.Application.Calendar;
using Adeni.Domain.Calendar;
using Adeni.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

public sealed class CalendarReconciliationJob(AdeniDbContext db, CalendarConnectionSession session,
    ICalendarBookingQuery bookings, IOptions<CalendarOptions> options, TimeProvider clock, ILogger<CalendarReconciliationJob> logger, IBackgroundJobClient? jobs = null, Adeni.Application.Tenancy.IBookingPreselectionQuery? eligibility = null)
{
    [DisableConcurrentExecution(60)]
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var google = options.Value.Google.Enabled; var microsoft = options.Value.Microsoft.Enabled;
        var due = await db.Set<CalendarConnection>().AsNoTracking().Where(x => x.Status == "connected"
            && ((x.Provider == "google" && google) || (x.Provider == "microsoft" && microsoft))
            && x.NextSyncAt <= clock.GetUtcNow() && (x.LeaseUntil == null || x.LeaseUntil < clock.GetUtcNow()))
            .OrderBy(x => x.NextSyncAt).Take(20).Select(x => new { x.TenantId, x.Id }).ToArrayAsync(ct);
        foreach (var item in due)
        {
            if (!options.Value.Google.Enabled && !options.Value.Microsoft.Enabled) break;
            jobs?.Enqueue<CalendarReconciliationJob>(worker => worker.SynchronizeAsync(item.TenantId, item.Id, CancellationToken.None));
        }
        // Short-lived authorization material is never retained as an integration history.
        var expired = await db.Set<CalendarOAuthAttempt>().Where(x => x.ExpiresAt < clock.GetUtcNow()).OrderBy(x => x.ExpiresAt).Take(200).ToArrayAsync(ct);
        db.RemoveRange(expired); await db.SaveChangesAsync(ct);
    }

    public async Task SynchronizeAsync(Guid tenant, Guid id, CancellationToken ct)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct); budget.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            var (connection, provider, grant) = await session.OpenAsync(tenant, id, budget.Token);
            var selections = await db.Set<CalendarSelection>().AsNoTracking().Where(x => x.TenantId == tenant && x.ConnectionId == id).ToArrayAsync(budget.Token);
            if (selections.All(x => x.IsWriteTarget)) { connection.NextSyncAt = clock.GetUtcNow().AddMinutes(5); await session.ReleaseAsync(connection, budget.Token); return; }
            var sources = selections.Where(x => !x.IsWriteTarget).Select(x => new CalendarSelectionView(x.ExternalId, x.TimeZoneId, false)).ToArray();
            var today = new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
            var start = today.AddDays(-1); var end = today.AddDays(91);
            var snapshots = await db.Set<CalendarSnapshot>().Where(x => x.TenantId == tenant && x.ConnectionId == id).ToArrayAsync(budget.Token);
            var rolling = snapshots.SingleOrDefault(x => x.StartAt == start && x.EndAt == end);
            if (rolling == null) { rolling = new CalendarSnapshot { Id = Guid.NewGuid(), TenantId = tenant, ConnectionId = id, StartAt = start, EndAt = end }; db.Add(rolling); }
            var retain = snapshots.Where(x => x.EndAt > today && !(x.StartAt < start && x.EndAt <= end) && x != rolling).Take(12).Append(rolling).ToArray();
            var staleIds = snapshots.Where(x => !retain.Contains(x)).Select(x => x.Id).ToArray();
            db.RemoveRange(await db.Set<ExternalBusyBlock>().Where(x => x.TenantId == tenant && staleIds.Contains(x.SnapshotId)).ToArrayAsync(budget.Token));
            db.RemoveRange(snapshots.Where(x => staleIds.Contains(x.Id)));
            foreach (var snapshot in retain)
            {
                var blocks = await provider.BusyAsync(grant, sources, snapshot.StartAt, snapshot.EndAt, budget.Token);
                if (blocks.Any(x => x.EndAt <= x.StartAt || x.StartAt.Offset != TimeSpan.Zero || x.EndAt.Offset != TimeSpan.Zero))
                    throw new CalendarProviderException("response_invalid");
                var old = await db.Set<ExternalBusyBlock>().Where(x => x.TenantId == tenant && x.SnapshotId == snapshot.Id).ToArrayAsync(budget.Token);
                db.RemoveRange(old);
                foreach (var interval in blocks.Distinct()) db.Add(new ExternalBusyBlock { Id = Guid.NewGuid(), TenantId = tenant, SnapshotId = snapshot.Id, StartAt = interval.StartAt, EndAt = interval.EndAt });
                snapshot.SyncedAt = clock.GetUtcNow();
            }
            var guard = await db.Set<CalendarStaffState>().SingleAsync(x => x.TenantId == tenant && x.Id == connection.StaffMemberId, budget.Token);
            guard.AdmissionStamp = Guid.NewGuid();
            connection.FailureCategory = null;
            await session.SaveFencedAsync(connection, budget.Token); // Complete source snapshots + guard publish atomically; never a partial free window.
            var mapped = await db.Set<ExternalBookingEvent>().Where(x => x.TenantId == tenant && x.ConnectionId == id && x.State != "deleted" && x.State != "archived").OrderBy(x => x.Id).Take(1000).ToArrayAsync(budget.Token);
            if (mapped.Length == 1000 && await db.Set<ExternalBookingEvent>().CountAsync(x => x.TenantId == tenant && x.ConnectionId == id && x.State != "deleted" && x.State != "archived", budget.Token) > 1000)
                throw new CalendarProviderException("projection_limit");
            var factsById = new Dictionary<Guid, CalendarBookingFact>();
            foreach (var window in retain)
            {
                var known = window == retain[0] ? mapped.Select(x => x.BookingId).ToArray() : Array.Empty<Guid>();
                foreach (var fact in await bookings.ReadAsync(tenant, window.StartAt, window.EndAt, known, budget.Token)) factsById[fact.Id] = fact;
            }
            var facts = factsById.Values.ToArray();
            if (facts.Count(x => x.StaffMemberId == connection.StaffMemberId && x.ShouldWrite) > 1000)
                throw new CalendarProviderException("projection_limit");
            await DetectConflictsAsync(connection, facts, budget.Token);
            var output = selections.SingleOrDefault(x => x.IsWriteTarget);
            if (output != null && connection.WriteAccess && options.Value.Provider(connection.Provider).WriteBackEnabled)
                await WriteBackAsync(connection, provider, grant, output.ExternalId, facts, mapped, budget.Token);
            connection.FailureCount = 0;
            connection.NextSyncAt = clock.GetUtcNow().AddMinutes(5).AddSeconds(Random.Shared.Next(0, 30));
            await session.ReleaseAsync(connection, budget.Token);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            var category = ex is CalendarProviderException known ? known.Category : ex is DbUpdateConcurrencyException ? "concurrent_change" : "sync_failed";
            // Discard all unpublished snapshot changes. Do not persist raw provider errors or secrets in Hangfire failures.
            await session.FailAsync(category, ex is CalendarProviderException failure ? failure.RetryAfter : null, CancellationToken.None);
            logger.LogWarning("Calendar synchronization connection={ConnectionId} status={Category}", id, category);
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
        }
    }
    private async Task DetectConflictsAsync(CalendarConnection c, IReadOnlyList<CalendarBookingFact> facts, CancellationToken ct)
    {
        var blocks = await (from snapshot in db.Set<CalendarSnapshot>().AsNoTracking()
            join block in db.Set<ExternalBusyBlock>().AsNoTracking() on snapshot.Id equals block.SnapshotId
            where snapshot.TenantId == c.TenantId && block.TenantId == c.TenantId && snapshot.ConnectionId == c.Id
            select block).ToArrayAsync(ct);
        var existing = await db.Set<CalendarConflict>().Where(x => x.TenantId == c.TenantId && x.StaffMemberId == c.StaffMemberId).ToArrayAsync(ct);
        foreach (var fact in facts.Where(x => x.StaffMemberId == c.StaffMemberId || x.StaffMemberId == null))
        {
            var hit = (fact.BlocksAvailability || fact.IsInProgress) && blocks.Any(x => x.StartAt < fact.EndAt && x.EndAt > fact.StartAt);
            if (hit && fact.StaffMemberId == null && eligibility != null && fact.ServiceId != null && fact.LocationId != null)
                hit = await eligibility.IsValidAsync(c.TenantId, fact.LocationId.Value, fact.ServiceId, c.StaffMemberId, ct);
            var conflict = existing.SingleOrDefault(x => x.BookingId == fact.Id);
            if (hit && conflict == null) db.Add(new CalendarConflict { Id = Guid.NewGuid(), TenantId = c.TenantId, StaffMemberId = c.StaffMemberId, BookingId = fact.Id, DetectedAt = clock.GetUtcNow(), Category = fact.StaffMemberId == null ? "unassigned_capacity" : "assigned_overlap", StartAt = fact.StartAt, EndAt = fact.EndAt });
            else if (hit && conflict != null) { conflict.ResolvedAt = null; conflict.StartAt = fact.StartAt; conflict.EndAt = fact.EndAt; }
            else if (conflict != null) conflict.ResolvedAt ??= clock.GetUtcNow();
        }
        await session.SaveFencedAsync(c, ct);
    }
    private async Task WriteBackAsync(CalendarConnection c, IExternalCalendarProvider provider, CalendarGrant grant, string target,
        IReadOnlyList<CalendarBookingFact> facts, IReadOnlyList<ExternalBookingEvent> mapped, CancellationToken ct)
    {
        foreach (var fact in facts.Where(x => x.StaffMemberId == c.StaffMemberId && x.ShouldWrite || mapped.Any(m => m.BookingId == x.Id)))
        {
            var mapping = mapped.SingleOrDefault(x => x.BookingId == fact.Id);
            var wanted = fact.StaffMemberId == c.StaffMemberId && fact.ShouldWrite;
            // Completed/paid historical appointments cannot be cancelled through normal Booking operations.
            // Keep their copy and mapping, without letting lifetime history consume the active reconciliation bound.
            if (mapping != null && fact.IsFinal && fact.EndAt < clock.GetUtcNow().AddDays(-1))
            { mapping.State = "archived"; await session.SaveFencedAsync(c, ct); continue; }
            if (mapping == null)
            {
                if (!wanted) continue;
                mapping = new ExternalBookingEvent { Id = Guid.NewGuid(), TenantId = c.TenantId, BookingId = fact.Id, ConnectionId = c.Id, TargetId = target };
                db.Add(mapping); await session.SaveFencedAsync(c, ct);
            }
            if (!wanted)
            {
                if (mapping.State == "deleted") continue;
                await provider.DeleteAsync(grant, mapping.TargetId, mapping.Id, mapping.ExternalId, ct);
                mapping.State = "deleted"; await session.SaveFencedAsync(c, ct); continue;
            }
            var allowCreate = mapping.State is "pending" or "deleted";
            // Persist intent before HTTP. An ambiguous Microsoft create is recovered by marker lookup, never blindly posted again.
            mapping.State = "creating"; await session.SaveFencedAsync(c, ct);
            var result = await provider.WriteAsync(grant, mapping.TargetId, mapping.Id, mapping.ExternalId, fact.StartAt, fact.EndAt, allowCreate, ct);
            if (string.IsNullOrEmpty(result.ExternalId) || result.ExternalId.Length > 2048) throw new CalendarProviderException("response_invalid");
            mapping.ExternalId = result.ExternalId; mapping.State = "written";
            mapping.AppliedFingerprint = CalendarCredentialProtector.Hash($"{fact.StartAt:O}|{fact.EndAt:O}|{fact.StaffMemberId}");
            await session.SaveFencedAsync(c, ct);
        }
    }
}
