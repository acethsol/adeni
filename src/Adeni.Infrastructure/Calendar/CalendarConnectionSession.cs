namespace Adeni.Infrastructure.Calendar;

using System.Text.Json;
using Adeni.Application.Calendar;
using Adeni.Domain.Calendar;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

/// <summary>SQL rowversion + fenced lease serializes refresh, provider calls and disconnect. No SQL transaction spans HTTP.</summary>
public sealed class CalendarConnectionSession(AdeniDbContext db, IEnumerable<IExternalCalendarProvider> providers,
    CalendarCredentialProtector protector, IOptions<CalendarOptions> options, TimeProvider clock)
{
    private Guid? currentLease;
    private Guid currentTenant;
    private Guid currentConnection;
    public async Task<(CalendarConnection Connection, IExternalCalendarProvider Provider, CalendarGrant Grant)> OpenAsync(Guid tenant, Guid id, CancellationToken ct)
    {
        currentLease = null;
        var c = await db.Set<CalendarConnection>().SingleOrDefaultAsync(x => x.TenantId == tenant && x.Id == id, ct)
            ?? throw new CalendarProviderException("not_found");
        if (c.Status == "disconnected" || !options.Value.Provider(c.Provider).Enabled) throw new CalendarProviderException("integration_disabled");
        if (c.LeaseUntil > clock.GetUtcNow()) throw new CalendarProviderException("sync_in_progress");
        c.LeaseId = Guid.NewGuid(); c.LeaseUntil = clock.GetUtcNow().AddMinutes(3);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new CalendarProviderException("sync_in_progress"); }
        currentLease = c.LeaseId; currentTenant = tenant; currentConnection = id;
        var provider = providers.Single(x => x.Provider == c.Provider);
        var grant = JsonSerializer.Deserialize<CalendarGrant>(protector.Unprotect(tenant, id, c.Provider, c.ProtectedCredentials))
            ?? throw new CalendarProviderException("credential_unavailable");
        if (grant.ExpiresAt < clock.GetUtcNow().AddMinutes(2))
        {
            grant = await provider.RefreshAsync(grant, ct);
            c.ProtectedCredentials = protector.Protect(tenant, id, c.Provider, JsonSerializer.Serialize(grant));
            c.TokenExpiresAt = grant.ExpiresAt;
            await db.SaveChangesAsync(ct); // CAS prevents a late refresh resurrecting disconnected credentials.
        }
        return (c, provider, grant);
    }
    public async Task FailAsync(string category, TimeSpan? retryAfter, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        if (currentLease == null) return;
        var c = await db.Set<CalendarConnection>().SingleOrDefaultAsync(x => x.TenantId == currentTenant && x.Id == currentConnection
            && x.LeaseId == currentLease && x.Status != "disconnected", ct);
        if (c == null) { currentLease = null; return; }
        c.FailureCategory = category; c.LeaseId = null; c.LeaseUntil = null;
        if (category is "reconnect_required" or "consent_required" or "credential_unavailable") c.Status = "reconnect_required";
        c.FailureCount = Math.Min(c.FailureCount + 1, 10);
        var delay = retryAfter.HasValue ? TimeSpan.FromSeconds(Math.Max(60, retryAfter.Value.TotalSeconds))
            : TimeSpan.FromMinutes(Math.Min(60, 5 * Math.Pow(2, c.FailureCount - 1))).Add(TimeSpan.FromSeconds(Random.Shared.Next(0,30)));
        try { c.NextSyncAt = clock.GetUtcNow().Add(delay); }
        catch (ArgumentOutOfRangeException) { c.NextSyncAt = DateTimeOffset.MaxValue; }
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); }
        currentLease = null;
    }
    public async Task SaveFencedAsync(CalendarConnection c, CancellationToken ct)
    {
        if (c.LeaseId == null || c.LeaseUntil <= clock.GetUtcNow()) throw new CalendarProviderException("lease_expired");
        // Every post-HTTP save must compare the connection rowversion, even when only child rows changed.
        db.Entry(c).Property(x => x.LeaseUntil).IsModified = true;
        await db.SaveChangesAsync(ct);
    }
    public async Task ReleaseAsync(CalendarConnection c, CancellationToken ct)
    {
        c.LeaseId = null; c.LeaseUntil = null;
        await db.SaveChangesAsync(ct);
        currentLease = null;
    }
}
