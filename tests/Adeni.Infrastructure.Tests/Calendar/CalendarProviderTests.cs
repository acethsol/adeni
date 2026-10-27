namespace Adeni.Infrastructure.Tests.Calendar;

using System.Net;
using System.Text;
using System.Text.Json;
using Adeni.Application.Calendar;
using Adeni.Infrastructure.Calendar;
using Microsoft.AspNetCore.WebUtilities;
using static Adeni.Infrastructure.Tests.Calendar.CalendarTestSupport;

public sealed class CalendarProviderTests
{
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Url,string? Authorization,string? Body)> Requests=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        { Requests.Add((request.RequestUri!.ToString(),request.Headers.Authorization?.ToString(),request.Content==null?null:await request.Content.ReadAsStringAsync(ct)));return respond(request); }
    }
    private static HttpResponseMessage Json(string json,HttpStatusCode status=HttpStatusCode.OK)=>new(status){Content=new StringContent(json,Encoding.UTF8,"application/json")};
    private static CalendarGrant Grant()=>new(){AccessToken="sensitive-token",RefreshToken="sensitive-refresh",ExpiresAt=Now.AddHours(1)};
    [Fact]
    public void OAuthUrls_UsePkceStateNonceAndFixedCallbacks_WithSeparateWriteConsent()
    {
        using var client=new HttpClient(new Handler(_=>Json("{}")));var p=new GoogleCalendarProvider(client,Options(),new Clock());
        var query=QueryHelpers.ParseQuery(new Uri(p.AuthorizationUrl("state","challenge","nonce",false)).Query);
        Assert.Equal("S256",query["code_challenge_method"]);Assert.Equal("challenge",query["code_challenge"]);Assert.Equal("nonce",query["nonce"]);Assert.Equal("state",query["state"]);
        Assert.Equal("https://api.example.test/api/v1/calendar/oauth/google/callback",query["redirect_uri"]);
        Assert.DoesNotContain("calendar.events ",query["scope"].ToString());Assert.Contains("calendar.events",new Uri(p.AuthorizationUrl("s","c","n",true)).Query);
    }
    [Fact]
    public async Task OAuthExchange_ValidatesSignedIdentityAndNonce_BeforeAcceptingGrant()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var key = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa) { KeyId = "test-key" };
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken("https://accounts.google.com", "test",
            [new("sub", "provider-subject"), new("nonce", "expected-nonce")], DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10),
            new Microsoft.IdentityModel.Tokens.SigningCredentials(key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256));
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token); var parameters = rsa.ExportParameters(false);
        var handler = new Handler(request => request.RequestUri!.AbsolutePath.Contains(".well-known")
            ? Json("{\"issuer\":\"https://accounts.google.com\",\"jwks_uri\":\"https://www.googleapis.com/oauth2/v3/certs\"}")
            : request.RequestUri.AbsolutePath.Contains("certs")
            ? Json(JsonSerializer.Serialize(new { keys = new[] { new { kty = "RSA", kid = "test-key", use = "sig", alg = "RS256", n = WebEncoders.Base64UrlEncode(parameters.Modulus!), e = WebEncoders.Base64UrlEncode(parameters.Exponent!) } } }))
            : Json(JsonSerializer.Serialize(new { access_token = "sensitive-access", refresh_token = "sensitive-refresh", expires_in = 3600,
                scope = "https://www.googleapis.com/auth/calendar.calendarlist.readonly https://www.googleapis.com/auth/calendar.events.freebusy", id_token = jwt })));
        using var http = new HttpClient(handler); var provider = new GoogleCalendarProvider(http, Options(), new Clock());
        var grant = await provider.ExchangeAsync("code", "verifier", "expected-nonce", false, default); Assert.Equal("provider-subject", grant.AccountId);
        var failure = await Assert.ThrowsAsync<CalendarProviderException>(() => provider.ExchangeAsync("code", "verifier", "wrong-nonce", false, default));
        Assert.Equal("identity_invalid", failure.Category); Assert.DoesNotContain("sensitive-", failure.ToString());
    }
    [Fact]
    public async Task GoogleFreeBusy_UsesOnlySelectedCalendarAndNoEventContent_AndRejectsPartialErrors()
    {
        var handler=new Handler(_=>Json("{\"calendars\":{\"busy\":{\"busy\":[{\"start\":\"2026-10-25T13:00:00Z\",\"end\":\"2026-10-25T14:00:00Z\"}]}}}"));
        using var http=new HttpClient(handler);var p=new GoogleCalendarProvider(http,Options(),new Clock());
        var intervals=await p.BusyAsync(Grant(),[new("busy","UTC",false)],Now,Now.AddDays(1),default);
        Assert.Single(intervals);Assert.EndsWith("/freeBusy",Assert.Single(handler.Requests).Url);Assert.DoesNotContain("sensitive-token",handler.Requests[0].Body!);
        using var badHttp=new HttpClient(new Handler(_=>Json("{\"calendars\":{\"busy\":{\"errors\":[{\"reason\":\"sensitive-provider-detail\"}]}}}")));
        var error=await Assert.ThrowsAsync<CalendarProviderException>(()=>new GoogleCalendarProvider(badHttp,Options(),new Clock()).BusyAsync(Grant(),[new("busy","UTC",false)],Now,Now.AddDays(1),default));
        Assert.Equal("incomplete_sync",error.Category);Assert.DoesNotContain("sensitive-provider-detail",error.ToString());
    }
    [Fact]
    public async Task GraphCalendarView_ExpandsProviderInstances_AndRetainsOnlyBlockingIntervals()
    {
        var events=new[]{new{id="a",showAs="busy",isCancelled=false,start=new{dateTime="2026-10-25T13:00:00",timeZone="UTC"},end=new{dateTime="2026-10-25T14:00:00",timeZone="UTC"}},
            new{id="b",showAs="free",isCancelled=false,start=new{dateTime="2026-10-25T13:00:00",timeZone="UTC"},end=new{dateTime="2026-10-25T14:00:00",timeZone="UTC"}},
            new{id="c",showAs="tentative",isCancelled=true,start=new{dateTime="2026-10-25T13:00:00",timeZone="UTC"},end=new{dateTime="2026-10-25T14:00:00",timeZone="UTC"}}};
        var handler=new Handler(_=>Json(JsonSerializer.Serialize(new{value=events})));using var http=new HttpClient(handler);var p=new MicrosoftCalendarProvider(http,Options(),new Clock());
        Assert.Single(await p.BusyAsync(Grant(),[new("selected","UTC",false)],Now,Now.AddDays(1),default));
        var request=Assert.Single(handler.Requests);Assert.Contains("/selected/calendarView",request.Url);var query=QueryHelpers.ParseQuery(new Uri(request.Url).Query);
        Assert.DoesNotContain("subject",query["$select"].ToString());Assert.DoesNotContain("attendees",query["$select"].ToString());Assert.DoesNotContain("body",query["$select"].ToString());
    }
    [Fact]
    public async Task GraphPagination_DoesNotForwardBearerToUntrustedHost()
    {
        var handler=new Handler(_=>Json("{\"value\":[],\"@odata.nextLink\":\"https://evil.example.test/steal\"}"));using var http=new HttpClient(handler);var p=new MicrosoftCalendarProvider(http,Options(),new Clock());
        var e=await Assert.ThrowsAsync<CalendarProviderException>(()=>p.CalendarsAsync(Grant(),default));Assert.Equal("endpoint_invalid",e.Category);Assert.Single(handler.Requests);
    }
    [Theory][InlineData(HttpStatusCode.Unauthorized,"reconnect_required")][InlineData(HttpStatusCode.TooManyRequests,"rate_limited")][InlineData(HttpStatusCode.ServiceUnavailable,"provider_failure")]
    public async Task ProviderErrors_RedactBodiesAndRetainSafeCategory(HttpStatusCode status,string category)
    {
        using var http=new HttpClient(new Handler(_=>Json("sensitive-refresh-provider-payload",status)));var p=new GoogleCalendarProvider(http,Options(),new Clock());
        var e=await Assert.ThrowsAsync<CalendarProviderException>(()=>p.CalendarsAsync(Grant(),default));Assert.Equal(category,e.Category);Assert.DoesNotContain("sensitive",e.ToString());Assert.Null(e.InnerException);
    }
    [Fact]
    public async Task GraphAmbiguousCreate_ReconcilesMarker_WithoutBlindSecondPost()
    {
        var handler=new Handler(_=>Json("{\"value\":[]}"));using var http=new HttpClient(handler);var p=new MicrosoftCalendarProvider(http,Options(),new Clock());
        var e=await Assert.ThrowsAsync<CalendarProviderException>(()=>p.WriteAsync(Grant(),"output",Guid.NewGuid(),null,Now,Now.AddHours(1),false,default));
        Assert.Equal("create_unresolved",e.Category);Assert.All(handler.Requests,x=>Assert.Null(x.Body));
    }
    [Fact]
    public async Task GoogleWrite_VerifiesOwnershipBeforeUpdatingForeignEvent()
    {
        var handler=new Handler(_=>Json("{\"id\":\"existing\",\"extendedProperties\":{\"private\":{\"adeniMapping\":\"someone-else\"}}}"));using var http=new HttpClient(handler);var p=new GoogleCalendarProvider(http,Options(),new Clock());
        var e=await Assert.ThrowsAsync<CalendarProviderException>(()=>p.WriteAsync(Grant(),"output",Guid.NewGuid(),null,Now,Now.AddHours(1),true,default));Assert.Equal("ownership_mismatch",e.Category);Assert.Single(handler.Requests);
    }
}
