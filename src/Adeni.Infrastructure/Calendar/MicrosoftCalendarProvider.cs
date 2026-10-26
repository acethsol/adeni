namespace Adeni.Infrastructure.Calendar;

using System.Globalization;
using System.Text.Json;
using Adeni.Application.Calendar;
using Microsoft.Extensions.Options;

public sealed class MicrosoftCalendarProvider(HttpClient http, IOptions<CalendarOptions> options, TimeProvider clock)
    : CalendarProviderHttp(http, options, clock)
{
    public override string Provider => "microsoft";
    private const string Api = "https://graph.microsoft.com/v1.0/me/calendars";
    private const string Marker = "String {8eccc264-cde4-4f40-b1a9-49a79cc68259} Name AdeniMapping";
    protected override string TokenUrl => "https://login.microsoftonline.com/common/oauth2/v2.0/token";
    protected override string[] RequiredScopes(bool write) => [write ? "Calendars.ReadWrite" : "Calendars.ReadBasic"];
    protected override string Scope(bool write) => "openid offline_access " + string.Join(' ', RequiredScopes(write));
    public override string AuthorizationUrl(string state, string challenge, string nonce, bool write) => Query("https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
        ("client_id", Settings.ClientId), ("redirect_uri", Options.Callback(Provider)), ("response_type", "code"), ("response_mode", "query"),
        ("scope", Scope(write)), ("state", state), ("code_challenge", challenge), ("code_challenge_method", "S256"), ("nonce", nonce), ("prompt", "select_account"));
    private async Task<IReadOnlyList<JsonElement>> Pages(string url, CalendarGrant grant, CancellationToken ct)
    {
        var result = new List<JsonElement>();
        for (var page = 0; page < 40; page++)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host != "graph.microsoft.com" || !uri.AbsolutePath.StartsWith("/v1.0/me/calendars", StringComparison.Ordinal))
                throw new CalendarProviderException("endpoint_invalid");
            using var json = await GetAsync(url, grant, ct);
            foreach (var item in json.RootElement.GetProperty("value").EnumerateArray()) result.Add(item.Clone());
            if (result.Count > 20000) throw new CalendarProviderException("response_limit");
            url = Text(json.RootElement, "@odata.nextLink");
            if (url.Length == 0) return result;
        }
        throw new CalendarProviderException("response_limit");
    }
    public override async Task<IReadOnlyList<ExternalCalendarView>> CalendarsAsync(CalendarGrant grant, CancellationToken ct) =>
        (await Pages(Query(Api, ("$select", "id,name,canEdit"), ("$top", "100")), grant, ct))
        .Select(x => new ExternalCalendarView(Text(x, "id"), Text(x, "name"), "UTC", x.TryGetProperty("canEdit", out var edit) && edit.GetBoolean())).ToArray();
    public override async Task<IReadOnlyList<CalendarBusyInterval>> BusyAsync(CalendarGrant grant, IReadOnlyList<CalendarSelectionView> selections,
        DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var result = new List<CalendarBusyInterval>();
        foreach (var selection in selections)
        for (var cursor = start; cursor < end; cursor = cursor.AddDays(14))
        {
            var until = cursor.AddDays(14) < end ? cursor.AddDays(14) : end;
            var events = await Pages(Query(Api + "/" + Q(selection.Id) + "/calendarView", ("startDateTime", Instant(cursor)),
                ("endDateTime", Instant(until)), ("$select", "id,start,end,showAs,isCancelled"), ("$top", "1000")), grant, ct);
            foreach (var ev in events)
            {
                if (ev.TryGetProperty("isCancelled", out var cancelled) && cancelled.GetBoolean()) continue;
                if (Text(ev, "showAs") is "free" or "workingElsewhere") continue;
                var a = ParseInstant(ev.GetProperty("start")); var b = ParseInstant(ev.GetProperty("end"));
                if (b <= a) throw new CalendarProviderException("response_invalid");
                result.Add(new(a, b));
                if (result.Count > 20000) throw new CalendarProviderException("response_limit");
            }
        }
        return result;
    }
    internal static DateTimeOffset ParseInstant(JsonElement value)
    {
        var text = Text(value, "dateTime"); var zone = Text(value, "timeZone");
        // Graph returns dateTime plus a separate named zone. Never use host-local DateTime parsing.
        if (DateTimeOffset.TryParseExact(text, ["yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var explicitOffset)
            && (text.EndsWith('Z') || text.LastIndexOf('+') > 10 || text.LastIndexOf('-') > 10)) return explicitOffset.ToUniversalTime();
        var local = DateTime.SpecifyKind(DateTime.Parse(text, CultureInfo.InvariantCulture), DateTimeKind.Unspecified);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
        if (tz.IsInvalidTime(local) || tz.IsAmbiguousTime(local)) throw new CalendarProviderException("timezone_ambiguous");
        return new DateTimeOffset(local, tz.GetUtcOffset(local)).ToUniversalTime();
    }
    private static string EventsUrl(string calendar) => Api + "/" + Q(calendar) + "/events";
    private static string MarkerExpand => "singleValueExtendedProperties($filter=id eq '" + Marker + "')";
    private static bool Owned(JsonElement json, Guid mapping) => json.TryGetProperty("singleValueExtendedProperties", out var props)
        && props.EnumerateArray().Any(x => Text(x, "id") == Marker && Text(x, "value") == mapping.ToString("N"));
    private async Task<string?> FindOwned(CalendarGrant grant, string calendar, Guid mapping, string? externalId, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(externalId))
        {
            using var existing = await GetAsync(Query(EventsUrl(calendar) + "/" + Q(externalId), ("$select", "id"), ("$expand", MarkerExpand)), grant, ct, true);
            if (Text(existing.RootElement, "id").Length == 0) return null;
            if (!Owned(existing.RootElement, mapping)) throw new CalendarProviderException("ownership_mismatch");
            return Text(existing.RootElement, "id");
        }
        var matches = await Pages(Query(EventsUrl(calendar), ("$filter", "singleValueExtendedProperties/Any(ep: ep/id eq '" + Marker + "' and ep/value eq '" + mapping.ToString("N") + "')"),
            ("$select", "id"), ("$expand", MarkerExpand)), grant, ct);
        if (matches.Count > 1) throw new CalendarProviderException("duplicate_projection");
        return matches.Count == 0 ? null : Owned(matches[0], mapping) ? Text(matches[0], "id") : throw new CalendarProviderException("ownership_mismatch");
    }
    public override async Task<CalendarWriteResult> WriteAsync(CalendarGrant grant, string calendarId, Guid mappingId, string? externalId,
        DateTimeOffset start, DateTimeOffset end, bool allowCreate, CancellationToken ct)
    {
        var found = await FindOwned(grant, calendarId, mappingId, externalId, ct);
        if (found == null && !allowCreate) throw new CalendarProviderException("create_unresolved");
        var startValue = new { dateTime = start.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), timeZone = "UTC" };
        var endValue = new { dateTime = end.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), timeZone = "UTC" };
        var properties = new[] { new { id = Marker, value = mappingId.ToString("N") } };
        object body = found == null
            ? new { subject = "Adeni appointment", transactionId = mappingId.ToString("N"), start = startValue, end = endValue, singleValueExtendedProperties = properties }
            : new { subject = "Adeni appointment", start = startValue, end = endValue, singleValueExtendedProperties = properties };
        using var result = await SendOwnedAsync(found == null ? HttpMethod.Post : HttpMethod.Patch,
            EventsUrl(calendarId) + (found == null ? "" : "/" + Q(found)), grant, body, ct);
        return new(Text(result.RootElement, "id"));
    }
    public override async Task DeleteAsync(CalendarGrant grant, string calendarId, Guid mappingId, string? externalId, CancellationToken ct)
    {
        var found = await FindOwned(grant, calendarId, mappingId, externalId, ct);
        if (found == null) return;
        using var deleted = await SendOwnedAsync(HttpMethod.Delete, EventsUrl(calendarId) + "/" + Q(found), grant, null, ct, true);
    }
}
