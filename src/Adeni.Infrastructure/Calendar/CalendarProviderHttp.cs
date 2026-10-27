namespace Adeni.Infrastructure.Calendar;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Adeni.Application.Calendar;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

public abstract class CalendarProviderHttp(HttpClient http, IOptions<CalendarOptions> options, TimeProvider clock) : IExternalCalendarProvider
{
    public abstract string Provider { get; }
    protected CalendarOptions Options => options.Value;
    protected CalendarProviderOptions Settings => Options.Provider(Provider);
    protected abstract string TokenUrl { get; }
    protected abstract string Scope(bool write);
    public abstract string AuthorizationUrl(string state, string challenge, string nonce, bool write);
    protected static string Q(string value) => Uri.EscapeDataString(value);
    protected static string Instant(DateTimeOffset instant) => instant.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    protected static string Text(JsonElement json, string name) => json.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
    protected static string Query(string url, params (string Key, string Value)[] args) => url + "?" + string.Join('&', args.Select(a => Q(a.Key) + "=" + Q(a.Value)));

    public async Task<CalendarGrant> ExchangeAsync(string code, string verifier, string nonce, bool write, CancellationToken ct)
    {
        using var json = await TokenAsync(new() { ["grant_type"] = "authorization_code", ["code"] = code,
            ["code_verifier"] = verifier, ["redirect_uri"] = Options.Callback(Provider), ["scope"] = Scope(write) }, ct);
        var grant = ReadGrant(json.RootElement);
        var identity = Text(json.RootElement, "id_token");
        if (identity.Length == 0) throw new CalendarProviderException("identity_invalid");
        grant.AccountId = await ValidateIdentityAsync(identity, nonce, ct);
        if (grant.RefreshToken.Length == 0 || !RequiredScopes(write).All(s => grant.Scopes.Split(' ').Contains(s, StringComparer.Ordinal)))
            throw new CalendarProviderException("consent_required");
        return grant;
    }
    protected abstract string[] RequiredScopes(bool write);
    public async Task<CalendarGrant> RefreshAsync(CalendarGrant grant, CancellationToken ct)
    {
        using var json = await TokenAsync(new() { ["grant_type"] = "refresh_token", ["refresh_token"] = grant.RefreshToken }, ct);
        var refreshed = ReadGrant(json.RootElement);
        refreshed.AccountId = grant.AccountId;
        if (refreshed.RefreshToken.Length == 0) refreshed.RefreshToken = grant.RefreshToken;
        if (refreshed.Scopes.Length == 0) refreshed.Scopes = grant.Scopes;
        return refreshed;
    }
    private async Task<JsonDocument> TokenAsync(Dictionary<string,string> fields, CancellationToken ct)
    {
        fields["client_id"] = Settings.ClientId; fields["client_secret"] = Settings.ClientSecret;
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl) { Content = new FormUrlEncodedContent(fields) };
        return await SendAsync(request, null, ct);
    }
    private CalendarGrant ReadGrant(JsonElement json)
    {
        if (!json.TryGetProperty("expires_in", out var exp) || !exp.TryGetInt32(out var seconds) || seconds <= 0 || seconds > 86400)
            throw new CalendarProviderException("response_invalid");
        var token = Text(json, "access_token");
        if (token.Length == 0 || token.Length > 12000) throw new CalendarProviderException("response_invalid");
        return new() { AccessToken = token, RefreshToken = Text(json, "refresh_token"), Scopes = Text(json, "scope"), ExpiresAt = clock.GetUtcNow().AddSeconds(seconds) };
    }
    private async Task<string> ValidateIdentityAsync(string jwt, string nonce, CancellationToken ct)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var metadata = "https://accounts.google.com/.well-known/openid-configuration";
            string[] issuers = ["https://accounts.google.com", "accounts.google.com"];
            string? tid = null;
            if (Provider == "microsoft")
            {
                var untrusted = handler.ReadJwtToken(jwt);
                if (!Guid.TryParse(untrusted.Claims.FirstOrDefault(x => x.Type == "tid")?.Value, out var tenant)) throw new SecurityTokenException();
                tid = tenant.ToString();
                metadata = $"https://login.microsoftonline.com/{tid}/v2.0/.well-known/openid-configuration";
                issuers = [$"https://login.microsoftonline.com/{tid}/v2.0"];
            }
            var config = await OpenIdConnectConfigurationRetriever.GetAsync(metadata, new HttpDocumentRetriever(http) { RequireHttps = true }, ct);
            var principal = handler.ValidateToken(jwt, new TokenValidationParameters { ValidateIssuer = true, ValidIssuers = issuers,
                ValidateAudience = true, ValidAudience = Settings.ClientId, ValidateLifetime = true, RequireExpirationTime = true,
                RequireSignedTokens = true, IssuerSigningKeys = config.SigningKeys, ClockSkew = TimeSpan.FromMinutes(1), ValidAlgorithms = [SecurityAlgorithms.RsaSha256] }, out _);
            if (principal.FindFirst("nonce")?.Value != nonce) throw new SecurityTokenException();
            var sub = principal.FindFirst("sub")?.Value;
            if (string.IsNullOrWhiteSpace(sub) || sub.Length > 256) throw new SecurityTokenException();
            return Provider == "microsoft" ? tid + ":" + sub : sub;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { throw new CalendarProviderException("identity_invalid"); }
    }
    protected async Task<JsonDocument> SendAsync(HttpRequestMessage request, CalendarGrant? grant, CancellationToken ct, bool allowMissing = false)
    {
        var uri = request.RequestUri!;
        if (uri.Scheme != "https" || uri.UserInfo.Length > 0 || uri.Port != 443 || !AllowedHost(uri.Host)) throw new CalendarProviderException("endpoint_invalid");
        if (grant != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", grant.AccessToken);
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (allowMissing && response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone) return JsonDocument.Parse("{}");
            if (!response.IsSuccessStatusCode)
            {
                var category = response.StatusCode switch { HttpStatusCode.Unauthorized => "reconnect_required", HttpStatusCode.Forbidden => "consent_required",
                    HttpStatusCode.TooManyRequests => "rate_limited", HttpStatusCode.BadRequest when grant == null => "reconnect_required", _ => "provider_failure" };
                var retry = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - clock.GetUtcNow());
                throw new CalendarProviderException(category, retry);
            }
            if (response.StatusCode == HttpStatusCode.NoContent) return JsonDocument.Parse("{}");
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var bytes = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(bytes, ct)) != 0)
            {
                if (buffer.Length + count > 2_000_000) throw new CalendarProviderException("response_limit");
                buffer.Write(bytes, 0, count);
            }
            buffer.Position = 0;
            return buffer.Length == 0 ? JsonDocument.Parse("{}") : await JsonDocument.ParseAsync(buffer, cancellationToken: ct);
        }
        catch (HttpRequestException) { throw new CalendarProviderException("provider_unavailable"); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new CalendarProviderException("provider_timeout"); }
        catch (JsonException) { throw new CalendarProviderException("response_invalid"); }
    }
    private bool AllowedHost(string host) => Provider == "google" ? host is "www.googleapis.com" or "oauth2.googleapis.com" : host is "graph.microsoft.com" or "login.microsoftonline.com";
    protected Task<JsonDocument> GetAsync(string url, CalendarGrant grant, CancellationToken ct, bool missing = false) => SendOwnedAsync(HttpMethod.Get, url, grant, null, ct, missing);
    protected async Task<JsonDocument> SendOwnedAsync(HttpMethod method, string url, CalendarGrant grant, object? body, CancellationToken ct, bool missing = false)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body != null) request.Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
        return await SendAsync(request, grant, ct, missing);
    }
    public abstract Task<IReadOnlyList<ExternalCalendarView>> CalendarsAsync(CalendarGrant grant, CancellationToken ct);
    public abstract Task<IReadOnlyList<CalendarBusyInterval>> BusyAsync(CalendarGrant grant, IReadOnlyList<CalendarSelectionView> selections, DateTimeOffset start, DateTimeOffset end, CancellationToken ct);
    public abstract Task<CalendarWriteResult> WriteAsync(CalendarGrant grant, string calendarId, Guid mappingId, string? externalId, DateTimeOffset start, DateTimeOffset end, bool allowCreate, CancellationToken ct);
    public abstract Task DeleteAsync(CalendarGrant grant, string calendarId, Guid mappingId, string? externalId, CancellationToken ct);
}
