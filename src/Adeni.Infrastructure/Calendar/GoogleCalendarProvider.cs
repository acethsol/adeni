namespace Adeni.Infrastructure.Calendar;

using System.Text.Json;
using Adeni.Application.Calendar;
using Microsoft.Extensions.Options;

public sealed class GoogleCalendarProvider(HttpClient http, IOptions<CalendarOptions> options, TimeProvider clock)
    : CalendarProviderHttp(http, options, clock)
{
    public override string Provider => "google";
    private const string Api = "https://www.googleapis.com/calendar/v3";
    protected override string TokenUrl => "https://oauth2.googleapis.com/token";
    protected override string[] RequiredScopes(bool write) => write
        ? ["https://www.googleapis.com/auth/calendar.calendarlist.readonly", "https://www.googleapis.com/auth/calendar.events.freebusy", "https://www.googleapis.com/auth/calendar.events"]
        : ["https://www.googleapis.com/auth/calendar.calendarlist.readonly", "https://www.googleapis.com/auth/calendar.events.freebusy"];
    protected override string Scope(bool write) => "openid " + string.Join(' ', RequiredScopes(write));
    public override string AuthorizationUrl(string state, string challenge, string nonce, bool write) => Query("https://accounts.google.com/o/oauth2/v2/auth",
        ("client_id", Settings.ClientId), ("redirect_uri", Options.Callback(Provider)), ("response_type", "code"),
        ("scope", Scope(write)), ("state", state), ("code_challenge", challenge), ("code_challenge_method", "S256"),
        ("nonce", nonce), ("access_type", "offline"), ("prompt", "consent"));
    public override async Task<IReadOnlyList<ExternalCalendarView>> CalendarsAsync(CalendarGrant grant, CancellationToken ct)
    {
        var result = new List<ExternalCalendarView>(); string? token = null;
        for (var page = 0; page < 10; page++)
        {
            using var json = await GetAsync(Query(Api + "/users/me/calendarList", ("maxResults", "100"),
                ("fields", "items(id,summary,timeZone,accessRole),nextPageToken"), ("pageToken", token ?? "")), grant, ct);
            foreach (var item in json.RootElement.GetProperty("items").EnumerateArray())
                result.Add(new(Text(item, "id"), Text(item, "summary"), Text(item, "timeZone"), Text(item, "accessRole") is "owner" or "writer"));
            token = Text(json.RootElement, "nextPageToken");
            if (token.Length == 0) return result;
        }
        throw new CalendarProviderException("response_limit");
    }
    public override async Task<IReadOnlyList<CalendarBusyInterval>> BusyAsync(CalendarGrant grant, IReadOnlyList<CalendarSelectionView> selections,
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var result = new List<CalendarBusyInterval>();
        // Keep provider windows bounded; publish only after every window/calendar succeeds.
        for (var cursor = start; cursor < end; cursor = cursor.AddDays(14))
        {
            var until = cursor.AddDays(14) < end ? cursor.AddDays(14) : end;
            using var json = await SendOwnedAsync(HttpMethod.Post, Api + "/freeBusy", grant,
                new { timeMin = Instant(cursor), timeMax = Instant(until), timeZone = "UTC", items = selections.Select(s => new { id = s.Id }).ToArray() }, ct);
            var calendars = json.RootElement.GetProperty("calendars");
            foreach (var selection in selections)
            {
                if (!calendars.TryGetProperty(selection.Id, out var calendar) || calendar.TryGetProperty("errors", out _))
                    throw new CalendarProviderException("incomplete_sync");
                foreach (var busy in calendar.GetProperty("busy").EnumerateArray())
                {
                    var a = DateTimeOffset.Parse(Text(busy, "start"), System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime();
                    var b = DateTimeOffset.Parse(Text(busy, "end"), System.Globalization.CultureInfo.InvariantCulture).ToUniversalTime();
                    if (b <= a) throw new CalendarProviderException("response_invalid");
                    result.Add(new(a, b));
                    if (result.Count > 20000) throw new CalendarProviderException("response_limit");
                }
            }
        }
        return result;
    }
    private static string EventUrl(string calendarId, Guid mappingId) => Api + "/calendars/" + Q(calendarId) + "/events/" + mappingId.ToString("N");
    private static bool Owned(JsonElement json, Guid mapping) => json.TryGetProperty("extendedProperties", out var ep)
        && ep.TryGetProperty("private", out var p) && Text(p, "adeniMapping") == mapping.ToString("N");
    public override async Task<CalendarWriteResult> WriteAsync(CalendarGrant grant, string calendarId, Guid mappingId, string? externalId,
        DateTimeOffset start, DateTimeOffset end, bool allowCreate, CancellationToken ct)
    {
        var url = EventUrl(calendarId, mappingId);
        using var existing = await GetAsync(url + "?fields=id,extendedProperties", grant, ct, true);
        var id = Text(existing.RootElement, "id");
        if (id.Length > 0 && !Owned(existing.RootElement, mappingId)) throw new CalendarProviderException("ownership_mismatch");
        var body = new { id = mappingId.ToString("N"), summary = "Adeni appointment", start = new { dateTime = Instant(start) },
            end = new { dateTime = Instant(end) }, extendedProperties = new { @private = new { adeniMapping = mappingId.ToString("N") } } };
        using var result = await SendOwnedAsync(id.Length > 0 ? HttpMethod.Put : HttpMethod.Post,
            (id.Length > 0 ? url : Api + "/calendars/" + Q(calendarId) + "/events") + "?sendUpdates=none", grant, body, ct);
        return new(Text(result.RootElement, "id"));
    }
    public override async Task DeleteAsync(CalendarGrant grant, string calendarId, Guid mappingId, string? externalId, CancellationToken ct)
    {
        var url = EventUrl(calendarId, mappingId);
        using var existing = await GetAsync(url + "?fields=id,extendedProperties", grant, ct, true);
        if (Text(existing.RootElement, "id").Length == 0) return;
        if (!Owned(existing.RootElement, mappingId)) throw new CalendarProviderException("ownership_mismatch");
        using var deleted = await SendOwnedAsync(HttpMethod.Delete, url + "?sendUpdates=none", grant, null, ct, true);
    }
}
