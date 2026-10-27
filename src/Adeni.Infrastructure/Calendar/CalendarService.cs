namespace Adeni.Infrastructure.Calendar;

using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Adeni.Application.Calendar;
using Adeni.Domain.Calendar;
using Adeni.Domain.Common;
using Adeni.Domain.Auditing;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.WebUtilities;

public sealed class CalendarService(AdeniDbContext db, ICalendarAccessQuery access, IEnumerable<IExternalCalendarProvider> providers,
    CalendarConnectionSession session, CalendarCredentialProtector protector, IOptions<CalendarOptions> options, TimeProvider clock) : ICalendarService
{
    private static Error Denied => Error.Forbidden("Calendar management requires an authorized owner or manager.");
    private static Error ProviderError(CalendarProviderException ex) => new("calendar.unavailable", null,
        new Dictionary<string,object?> { ["category"] = ex.Category });
    private void Audit(Guid tenant, string actor, string action, Guid entity) => db.AuditLogs.Add(new AuditLogRecord {
        Id = Guid.NewGuid(), ActorId = actor, Action = "calendar." + action, EntityType = "calendar", EntityId = entity.ToString(),
        OccurredAt = clock.GetUtcNow(), MetadataJson = JsonSerializer.Serialize(new { tenantId = tenant }) });

    public async Task<Result<IReadOnlyList<CalendarConnectionView>>> ListAsync(Guid tenant, string actor, CancellationToken ct)
    {
        if (!await access.CanManageAsync(tenant, actor, null, ct)) return Result.Failure<IReadOnlyList<CalendarConnectionView>>(Denied);
        var connections = await db.Set<CalendarConnection>().AsNoTracking().Where(x => x.TenantId == tenant).OrderBy(x => x.CreatedAt).Take(200).ToArrayAsync(ct);
        var selections = await db.Set<CalendarSelection>().AsNoTracking().Where(x => x.TenantId == tenant).ToArrayAsync(ct);
        var snapshots = await db.Set<CalendarSnapshot>().AsNoTracking().Where(x => x.TenantId == tenant && x.SyncedAt > DateTimeOffset.MinValue).ToArrayAsync(ct);
        return Result.Success<IReadOnlyList<CalendarConnectionView>>(connections.Select(c => {
            var latest = snapshots.Where(x => x.ConnectionId == c.Id).OrderByDescending(x => x.SyncedAt).FirstOrDefault();
            var status = c.Status == "connected" && latest == null && selections.Any(s => s.ConnectionId == c.Id && !s.IsWriteTarget) ? "syncing" : c.Status == "connected" && latest != null && latest.SyncedAt < clock.GetUtcNow().AddMinutes(-15) ? "stale" : c.Status;
            return new CalendarConnectionView(c.Id, c.StaffMemberId, c.Provider, status, latest?.SyncedAt, latest?.EndAt, c.FailureCategory,
                selections.Where(s => s.ConnectionId == c.Id).Select(s => new CalendarSelectionView(s.ExternalId, s.TimeZoneId, s.IsWriteTarget)).ToArray());
        }).ToArray());
    }
    public async Task<Result<CalendarAuthorizationView>> ConnectAsync(Guid tenant, string actor, CalendarConnectRequest request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(tenant, actor, request.StaffMemberId, ct)) return Result.Failure<CalendarAuthorizationView>(Denied);
        if (request.Provider is not ("google" or "microsoft")) return Result.Failure<CalendarAuthorizationView>(Error.Validation("Invalid calendar provider."));
        var config = options.Value.Provider(request.Provider);
        if (!config.Enabled || (request.WriteAccess && !config.WriteBackEnabled)) return Result.Failure<CalendarAuthorizationView>(new("calendar.unavailable"));
        if (await db.Set<CalendarOAuthAttempt>().CountAsync(x => x.TenantId == tenant && x.ExpiresAt > clock.GetUtcNow() && x.ConsumedAt == null, ct) >= 10)
            return Result.Failure<CalendarAuthorizationView>(Error.Conflict("Too many pending calendar connections."));
        var ticket = CalendarCredentialProtector.Secret(); var verifier = CalendarCredentialProtector.Secret(); var nonce = CalendarCredentialProtector.Secret();
        var attempt = new CalendarOAuthAttempt { Id = Guid.NewGuid(), TenantId = tenant, Actor = actor, StaffMemberId = request.StaffMemberId,
            Provider = request.Provider, StateHash = CalendarCredentialProtector.Hash(ticket), WriteAccess = request.WriteAccess,
            ExpiresAt = clock.GetUtcNow().AddMinutes(10) };
        attempt.ProtectedVerifier = protector.Protect(tenant, attempt.Id, request.Provider, verifier + ":" + nonce);
        db.Add(attempt); Audit(tenant, actor, "consent_started", attempt.Id); await db.SaveChangesAsync(ct);
        return Result.Success(new CalendarAuthorizationView(options.Value.ApiBaseUrl.TrimEnd('/') + "/api/v1/calendar/oauth/launch?ticket=" + ticket));
    }
    public async Task<Result<CalendarOAuthLaunch>> LaunchAsync(string ticket, CancellationToken ct)
    {
        if (ticket.Length != 43) return Result.Failure<CalendarOAuthLaunch>(new("calendar.authorization_invalid"));
        var hash = CalendarCredentialProtector.Hash(ticket);
        var a = await db.Set<CalendarOAuthAttempt>().SingleOrDefaultAsync(x => x.StateHash == hash && x.ConsumedAt == null && x.LaunchedAt == null && x.ExpiresAt > clock.GetUtcNow(), ct);
        if (a == null || !await access.CanManageAsync(a.TenantId, a.Actor, a.StaffMemberId, ct) || !options.Value.Provider(a.Provider).Enabled)
            return Result.Failure<CalendarOAuthLaunch>(new("calendar.authorization_invalid"));
        var proof = CalendarCredentialProtector.Secret(); a.CorrelationHash = CalendarCredentialProtector.Hash(proof); a.LaunchedAt = clock.GetUtcNow();
        string[] secret;
        try { secret = protector.Unprotect(a.TenantId, a.Id, a.Provider, a.ProtectedVerifier).Split(':'); }
        catch (CalendarProviderException ex) { return Result.Failure<CalendarOAuthLaunch>(ProviderError(ex)); }
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(secret[0])));
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return Result.Failure<CalendarOAuthLaunch>(new("calendar.authorization_invalid")); }
        return Result.Success(new CalendarOAuthLaunch(providers.Single(x => x.Provider == a.Provider).AuthorizationUrl(ticket, challenge, secret[1], a.WriteAccess), proof));
    }
    public async Task<Result<bool>> CallbackAsync(string provider, string state, string correlation, string code, CancellationToken ct)
    {
        if (state.Length != 43 || correlation.Length != 43 || code.Length is < 1 or > 8192 || provider is not ("google" or "microsoft")) return Result.Failure<bool>(new("calendar.authorization_invalid"));
        var hash = CalendarCredentialProtector.Hash(state);
        var a = await db.Set<CalendarOAuthAttempt>().SingleOrDefaultAsync(x => x.StateHash == hash && x.Provider == provider && x.LaunchedAt != null && x.ConsumedAt == null && x.ExpiresAt > clock.GetUtcNow(), ct);
        if (a == null || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a.CorrelationHash), Encoding.ASCII.GetBytes(CalendarCredentialProtector.Hash(correlation)))
            || !await access.CanManageAsync(a.TenantId, a.Actor, a.StaffMemberId, ct) || !options.Value.Provider(provider).Enabled)
            return Result.Failure<bool>(new("calendar.authorization_invalid"));
        a.ConsumedAt = clock.GetUtcNow();
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return Result.Failure<bool>(new("calendar.authorization_invalid")); }
        try
        {
            var material = protector.Unprotect(a.TenantId, a.Id, provider, a.ProtectedVerifier).Split(':');
            var grant = await providers.Single(p => p.Provider == provider).ExchangeAsync(code, material[0], material[1], a.WriteAccess, ct);
            // Roles may change while the provider round trip is in progress.
            if (!await access.CanManageAsync(a.TenantId, a.Actor, a.StaffMemberId, ct)) return Result.Failure<bool>(Denied);
            var c = await db.Set<CalendarConnection>().SingleOrDefaultAsync(x => x.TenantId == a.TenantId && x.StaffMemberId == a.StaffMemberId && x.Provider == provider, ct);
            var accountHash = CalendarCredentialProtector.Hash(provider + ":" + grant.AccountId);
            if (c != null && c.Status != "disconnected" && c.AccountHash != accountHash) return Result.Failure<bool>(Error.Conflict("Disconnect the existing calendar account first."));
            if (c?.LeaseUntil > clock.GetUtcNow()) return Result.Failure<bool>(Error.Conflict("Calendar synchronization is running; retry connection."));
            if (c == null) { c = new CalendarConnection { Id = Guid.NewGuid(), TenantId = a.TenantId, StaffMemberId = a.StaffMemberId, Provider = provider, CreatedAt = clock.GetUtcNow() }; db.Add(c); }
            c.AccountHash = accountHash; c.Generation = Guid.NewGuid(); c.Status = "connected"; c.WriteAccess = a.WriteAccess;
            c.ProtectedCredentials = protector.Protect(c.TenantId, c.Id, provider, JsonSerializer.Serialize(grant)); c.TokenExpiresAt = grant.ExpiresAt;
            c.FailureCategory = null; c.NextSyncAt = clock.GetUtcNow(); c.LeaseId = null; c.LeaseUntil = null;
            await TouchStaffAsync(c, ct); Audit(c.TenantId, a.Actor, "connected", c.Id); await db.SaveChangesAsync(ct);
            return Result.Success(true);
        }
        catch (CalendarProviderException ex) { await session.FailAsync(ex.Category, ex.RetryAfter, CancellationToken.None); return Result.Failure<bool>(ProviderError(ex)); }
        catch (DbUpdateException) { return Result.Failure<bool>(Error.Conflict("Calendar connection changed; retry.")); }
    }
    public async Task<Result<IReadOnlyList<ExternalCalendarView>>> CalendarsAsync(Guid tenant, string actor, Guid id, CancellationToken ct)
    {
        if (!await access.CanManageAsync(tenant, actor, null, ct)) return Result.Failure<IReadOnlyList<ExternalCalendarView>>(Denied);
        try
        {
            var (c, p, grant) = await session.OpenAsync(tenant, id, ct);
            var calendars = await p.CalendarsAsync(grant, ct); await session.ReleaseAsync(c, ct);
            return Result.Success(calendars);
        }
        catch (CalendarProviderException ex) { await session.FailAsync(ex.Category, ex.RetryAfter, CancellationToken.None); return Result.Failure<IReadOnlyList<ExternalCalendarView>>(ProviderError(ex)); }
        catch (DbUpdateConcurrencyException) { return Result.Failure<IReadOnlyList<ExternalCalendarView>>(Error.Conflict("Calendar connection changed.")); }
    }
    public async Task<Result<bool>> SelectAsync(Guid tenant, string actor, Guid id, CalendarSelectionRequest request, CancellationToken ct)
    {
        if (!await access.CanManageAsync(tenant, actor, null, ct)) return Result.Failure<bool>(Denied);
        if (request.Calendars == null || request.Calendars.Count is < 1 or > 6 || request.Calendars.Count(x => x.IsWriteTarget) > 1
            || request.Calendars.Count(x => !x.IsWriteTarget) is < 1 or > 5 || request.Calendars.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != request.Calendars.Count
            || request.Calendars.Any(x => string.IsNullOrWhiteSpace(x.Id) || x.Id.Length > 2048)) return Result.Failure<bool>(Error.Validation("Invalid calendar selection."));
        try
        {
            var (c, p, grant) = await session.OpenAsync(tenant, id, ct);
            var available = await p.CalendarsAsync(grant, ct);
            var chosen = request.Calendars.Select(s => (Selection: s, Calendar: available.SingleOrDefault(x => x.Id == s.Id))).ToArray();
            if (chosen.Any(x => x.Calendar == null || (x.Selection.IsWriteTarget && (!x.Calendar.CanWrite || !c.WriteAccess || !options.Value.Provider(c.Provider).WriteBackEnabled))))
            { await session.ReleaseAsync(c, ct); return Result.Failure<bool>(Error.Validation("Calendar access is not granted.")); }
            var otherSelections = await db.Set<CalendarSelection>().Where(x => x.TenantId == tenant && x.ConnectionId != c.Id
                && db.Set<CalendarConnection>().Any(o => o.Id == x.ConnectionId && o.TenantId == tenant && o.AccountHash == c.AccountHash && o.Status != "disconnected")).ToArrayAsync(ct);
            if (chosen.Any(x => otherSelections.Any(o => o.ExternalId == x.Selection.Id && o.IsWriteTarget != x.Selection.IsWriteTarget)))
            { await session.ReleaseAsync(c, ct); return Result.Failure<bool>(Error.Validation("A write calendar cannot also be a busy source.")); }
            if (await db.Set<ExternalBookingEvent>().AnyAsync(x => x.TenantId == tenant && x.ConnectionId == id && x.State != "deleted", ct))
            {
                var oldTarget = await db.Set<CalendarSelection>().Where(x => x.TenantId == tenant && x.ConnectionId == id && x.IsWriteTarget).Select(x => x.ExternalId).SingleOrDefaultAsync(ct);
                if (oldTarget != chosen.FirstOrDefault(x => x.Selection.IsWriteTarget).Selection?.Id)
                { await session.ReleaseAsync(c, ct); return Result.Failure<bool>(Error.Conflict("Disconnect before replacing an output calendar with existing written appointments.")); }
            }
            var prior = await db.Set<CalendarSelection>().Where(x => x.TenantId == tenant && x.ConnectionId == id).ToArrayAsync(ct);
            db.RemoveRange(prior.Where(x => !chosen.Any(c => c.Selection.Id == x.ExternalId)));
            foreach (var item in chosen)
            {
                TimeZoneInfo.FindSystemTimeZoneById(item.Calendar!.TimeZoneId);
                var row = prior.SingleOrDefault(x => x.ExternalId == item.Calendar.Id);
                if (row == null)
                {
                    row = new CalendarSelection { Id = Guid.NewGuid(), TenantId = tenant, ConnectionId = id, ExternalId = item.Calendar.Id, ExternalIdHash = CalendarCredentialProtector.Hash(item.Calendar.Id) };
                    db.Add(row);
                }
                row.TimeZoneId = item.Calendar.TimeZoneId; row.IsWriteTarget = item.Selection.IsWriteTarget;
            }
            db.RemoveRange(await db.Set<CalendarSnapshot>().Where(x => x.TenantId == tenant && x.ConnectionId == id).ToArrayAsync(ct));
            // Cascade busy blocks explicitly for InMemory parity; SQL enforces the same relationship.
            var oldIds = db.ChangeTracker.Entries<CalendarSnapshot>().Where(x => x.State == EntityState.Deleted).Select(x => x.Entity.Id).ToArray();
            db.RemoveRange(await db.Set<ExternalBusyBlock>().Where(x => x.TenantId == tenant && oldIds.Contains(x.SnapshotId)).ToArrayAsync(ct));
            c.Generation = Guid.NewGuid(); c.NextSyncAt = clock.GetUtcNow(); await TouchStaffAsync(c, ct); Audit(tenant, actor, "selection_changed", id);
            await session.ReleaseAsync(c, ct); return Result.Success(true);
        }
        catch (CalendarProviderException ex) { await session.FailAsync(ex.Category, ex.RetryAfter, CancellationToken.None); return Result.Failure<bool>(ProviderError(ex)); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return Result.Failure<bool>(new("calendar.unavailable")); }
        catch (DbUpdateException) { return Result.Failure<bool>(Error.Conflict("Calendar connection changed.")); }
    }
    internal async Task TouchStaffAsync(CalendarConnection c, CancellationToken ct)
    {
        var guard = await db.Set<CalendarStaffState>().SingleOrDefaultAsync(x => x.TenantId == c.TenantId && x.Id == c.StaffMemberId, ct);
        if (guard == null) db.Add(new CalendarStaffState { Id = c.StaffMemberId, TenantId = c.TenantId, AdmissionStamp = Guid.NewGuid() });
        else guard.AdmissionStamp = Guid.NewGuid();
    }
    public async Task<Result<bool>> DisconnectAsync(Guid tenant, string actor, Guid id, CancellationToken ct)
    {
        if (!await access.CanManageAsync(tenant, actor, null, ct)) return Result.Failure<bool>(Denied);
        var c = await db.Set<CalendarConnection>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id, ct);
        if (c == null) return Result.Failure<bool>(Error.NotFound("Calendar connection"));
        c.Status = "disconnected"; c.ProtectedCredentials = ""; c.Generation = Guid.NewGuid(); c.LeaseId = null; c.LeaseUntil = null;
        var snapshots = await db.Set<CalendarSnapshot>().Where(x => x.TenantId == tenant && x.ConnectionId == id).ToArrayAsync(ct);
        var ids = snapshots.Select(x => x.Id).ToArray(); db.RemoveRange(await db.Set<ExternalBusyBlock>().Where(x => x.TenantId == tenant && ids.Contains(x.SnapshotId)).ToArrayAsync(ct));
        db.RemoveRange(snapshots); db.RemoveRange(await db.Set<CalendarSelection>().Where(x => x.TenantId == tenant && x.ConnectionId == id).ToArrayAsync(ct));
        await TouchStaffAsync(c, ct); Audit(tenant, actor, "disconnected", id);
        try { await db.SaveChangesAsync(ct); return Result.Success(true); } catch (DbUpdateConcurrencyException) { return Result.Failure<bool>(Error.Conflict("Calendar connection changed; retry disconnect.")); }
    }
    public async Task<Result<bool>> SyncAsync(Guid tenant, string actor, Guid id, CancellationToken ct)
    {
        if (!await access.CanManageAsync(tenant, actor, null, ct)) return Result.Failure<bool>(Denied);
        var c = await db.Set<CalendarConnection>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id && x.Status != "disconnected", ct);
        if (c == null) return Result.Failure<bool>(Error.NotFound("Calendar connection"));
        if (c.FailureCategory == "rate_limited" && c.NextSyncAt > clock.GetUtcNow()) return Result.Failure<bool>(Error.Conflict("The provider requested a retry delay."));
        c.NextSyncAt = clock.GetUtcNow(); Audit(tenant, actor, "sync_requested", id);
        try { await db.SaveChangesAsync(ct); return Result.Success(true); } catch (DbUpdateConcurrencyException) { return Result.Failure<bool>(Error.Conflict("Calendar connection changed.")); }
    }
    public async Task<Result<IReadOnlyList<CalendarConflictView>>> ConflictsAsync(Guid tenant, string actor, CancellationToken ct)
    {
        if (!await access.CanManageAsync(tenant, actor, null, ct)) return Result.Failure<IReadOnlyList<CalendarConflictView>>(Denied);
        return Result.Success<IReadOnlyList<CalendarConflictView>>(await db.Set<CalendarConflict>().AsNoTracking().Where(x => x.TenantId == tenant)
            .OrderByDescending(x => x.DetectedAt).Take(200).Select(x => new CalendarConflictView(x.Id, x.StaffMemberId, x.BookingId, x.DetectedAt, x.AcknowledgedAt, x.ResolvedAt, x.Category, x.StartAt, x.EndAt)).ToArrayAsync(ct));
    }
    public async Task<Result<bool>> AcknowledgeAsync(Guid tenant, string actor, Guid id, CancellationToken ct)
    {
        if (!await access.CanManageAsync(tenant, actor, null, ct)) return Result.Failure<bool>(Denied);
        var conflict = await db.Set<CalendarConflict>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id, ct);
        if (conflict == null) return Result.Failure<bool>(Error.NotFound("Calendar conflict"));
        conflict.AcknowledgedAt ??= clock.GetUtcNow(); Audit(tenant, actor, "conflict_acknowledged", id); await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}
