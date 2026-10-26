namespace Adeni.Application.Calendar;

/// <summary>Server-only OAuth material. Never serialize in public responses, jobs or logs.</summary>
public sealed class CalendarGrant
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string Scopes { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public override string ToString() => "[Protected calendar grant]";
}
public sealed record CalendarWriteResult(string ExternalId);
public interface IExternalCalendarProvider
{
    string Provider { get; }
    string AuthorizationUrl(string state, string challenge, string nonce, bool write);
    Task<CalendarGrant> ExchangeAsync(string code, string verifier, string nonce, bool write, CancellationToken ct);
    Task<CalendarGrant> RefreshAsync(CalendarGrant grant, CancellationToken ct);
    Task<IReadOnlyList<ExternalCalendarView>> CalendarsAsync(CalendarGrant grant, CancellationToken ct);
    Task<IReadOnlyList<CalendarBusyInterval>> BusyAsync(CalendarGrant grant, IReadOnlyList<CalendarSelectionView> selections,
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    Task<CalendarWriteResult> WriteAsync(CalendarGrant grant, string calendarId, Guid mappingId, string? externalId,
        DateTimeOffset start, DateTimeOffset end, bool allowCreate, CancellationToken ct);
    Task DeleteAsync(CalendarGrant grant, string calendarId, Guid mappingId, string? externalId, CancellationToken ct);
}
