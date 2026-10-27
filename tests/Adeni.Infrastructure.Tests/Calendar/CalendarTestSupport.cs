namespace Adeni.Infrastructure.Tests.Calendar;

using Adeni.Application.Calendar;
using Adeni.Domain.Calendar;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Domain.Booking;
using Adeni.Infrastructure.Calendar;
using Adeni.Infrastructure.Identity;
using Adeni.Infrastructure.Booking;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

internal static class CalendarTestSupport
{
    internal static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    internal sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    internal static AdeniDbContext Memory() =>
        new(new DbContextOptionsBuilder<AdeniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new TenantContext());

    internal static IOptions<CalendarOptions> Options() => Microsoft.Extensions.Options.Options.Create(new CalendarOptions {
        ApiBaseUrl = "https://api.example.test", PortalBaseUrl = "https://portal.example.test", CurrentKeyVersion = "v1",
        EncryptionKeys = new() { ["v1"] = Convert.ToBase64String(Enumerable.Range(1,32).Select(i => (byte)i).ToArray()) },
        Google = new() { Enabled = true, WriteBackEnabled = true, ClientId = "test", ClientSecret = "test-secret" } });
    internal sealed class FakeProvider : IExternalCalendarProvider
    {
        public string Provider => "google";
        public string? Failure;
        public string? RefreshFailure;
        public TimeSpan? RetryAfter;
        public Func<Task>? BeforeBusy;
        public List<CalendarBusyInterval> Blocks = [];
        public int Writes; public int Deletes; public int Refreshes;
        public string AuthorizationUrl(string state, string challenge, string nonce, bool write) => "https://accounts.google.com/o/oauth2/v2/auth?state=" + state;
        public Task<CalendarGrant> ExchangeAsync(string code, string verifier, string nonce, bool write, CancellationToken ct) => Task.FromResult(new CalendarGrant { AccessToken = "sensitive-access", RefreshToken = "sensitive-refresh", AccountId = "external-account", ExpiresAt = Now.AddHours(1) });
        public Task<CalendarGrant> RefreshAsync(CalendarGrant grant, CancellationToken ct) { Refreshes++; if (RefreshFailure != null) throw new CalendarProviderException(RefreshFailure); grant.ExpiresAt = Now.AddHours(1); return Task.FromResult(grant); }
        public Task<IReadOnlyList<ExternalCalendarView>> CalendarsAsync(CalendarGrant grant, CancellationToken ct) => Task.FromResult<IReadOnlyList<ExternalCalendarView>>([new("busy", "Calendar", "America/Toronto", false), new("output", "Adeni", "America/Toronto", true)]);
        public async Task<IReadOnlyList<CalendarBusyInterval>> BusyAsync(CalendarGrant grant, IReadOnlyList<CalendarSelectionView> selections, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
        { if (BeforeBusy != null) await BeforeBusy(); if (Failure != null) throw new CalendarProviderException(Failure, RetryAfter); return Blocks; }
        public Task<CalendarWriteResult> WriteAsync(CalendarGrant grant, string calendarId, Guid mappingId, string? externalId, DateTimeOffset start, DateTimeOffset end, bool allowCreate, CancellationToken ct) { Writes++; return Task.FromResult(new CalendarWriteResult(mappingId.ToString("N"))); }
        public Task DeleteAsync(CalendarGrant grant, string calendarId, Guid mappingId, string? externalId, CancellationToken ct) { Deletes++; return Task.CompletedTask; }
    }
    internal static CalendarService Service(AdeniDbContext db, FakeProvider provider) => new(db, new CalendarAccessQuery(db), [provider],
        new(db, [provider], new(Options()), Options(), new Clock()), new(Options()), Options(), new Clock());
    internal static CalendarReconciliationJob Job(AdeniDbContext db, FakeProvider provider) => new(db,
        new(db, [provider], new(Options()), Options(), new Clock()), new CalendarBookingQuery(db), Options(), new Clock(), NullLogger<CalendarReconciliationJob>.Instance);
    internal static async Task<(Guid Tenant, Guid Staff, CalendarConnection Connection)> SeedCalendar(AdeniDbContext db)
    {
        var tenant = Guid.NewGuid(); var staff = Guid.NewGuid(); var now = Now;
        db.Add(new Tenant { Id = tenant, Name = "Calendar business", Status = TenantStatus.Verified });
        db.Add(new BusinessUser { Id = Guid.NewGuid(), TenantId = tenant, Role = "owner", Auth0Sub = "owner-" + tenant });
        db.Add(new StaffMember { Id = staff, TenantId = tenant, FirstName = "A", LastName = "B", DisplayName = "Practitioner" });
        db.Add(new CalendarStaffState { Id = staff, TenantId = tenant, AdmissionStamp = Guid.NewGuid() });
        var c = new CalendarConnection { Id = Guid.NewGuid(), TenantId = tenant, StaffMemberId = staff, Provider = "google", AccountHash = "account", Generation = Guid.NewGuid(), CreatedAt = now, NextSyncAt = now, TokenExpiresAt = now.AddHours(1), WriteAccess = true };
        c.ProtectedCredentials = new CalendarCredentialProtector(Options()).Protect(tenant, c.Id, c.Provider, System.Text.Json.JsonSerializer.Serialize(new CalendarGrant { AccessToken = "sensitive-access", RefreshToken = "sensitive-refresh", ExpiresAt = now.AddHours(1) }));
        db.Add(c); db.Add(new CalendarSelection { Id = Guid.NewGuid(), TenantId = tenant, ConnectionId = c.Id, ExternalId = "busy", ExternalIdHash = "busy", TimeZoneId = "America/Toronto" });
        await db.SaveChangesAsync(); return (tenant, staff, c);
    }
}
