namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Application.Tenancy;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class CalendarIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    public CalendarIntegrationTests(WebApplicationFactory<Program> f)=>factory=f.WithWebHostBuilder(b=>{b.UseEnvironment("Testing");b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{["ConnectionStrings:AdeniDb"]="",["Redis:ConnectionString"]="",["Auth0:Enabled"]="false",["App:PublicWebBaseUrl"]="https://discover.example.test"}));});
    private async Task<(Guid Tenant,Guid Location,Guid Service,string Slug,string Actor)> Seed(string role="owner")
    {
        var tenant=Guid.NewGuid();var location=Guid.NewGuid();var service=Guid.NewGuid();var slug="calendar-"+tenant.ToString("N");var actor="user-"+tenant;
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.Add(new Tenant{Id=tenant,Name="Beauty business",Status=TenantStatus.Verified});db.Add(new BusinessProfile{TenantId=tenant});db.Add(new BusinessLocation{Id=location,TenantId=tenant,Slug=slug,Name="Main",IsPrimary=true});
        db.Add(new BusinessUser{Id=Guid.NewGuid(),TenantId=tenant,Auth0Sub=actor,Role=role});db.Add(new ServiceOffering{Id=service,TenantId=tenant,Name="Service",DurationMinutes=30,PriceAmount=10,Currency="CAD"});await db.SaveChangesAsync();return(tenant,location,service,slug,actor);
    }
    private HttpClient Client(string? actor=null,Guid? tenant=null)
    {
        var c=factory.CreateClient(new(){AllowAutoRedirect=false});if(actor!=null)c.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub",actor);if(tenant!=null)c.DefaultRequestHeaders.Add("X-Tenant-Id",tenant.ToString());return c;
    }
    [Theory][InlineData("owner",HttpStatusCode.OK)][InlineData("manager",HttpStatusCode.OK)][InlineData("practitioner",HttpStatusCode.Forbidden)][InlineData("unknown",HttpStatusCode.Forbidden)][InlineData("ops",HttpStatusCode.Forbidden)]
    public async Task CalendarManagement_UsesStrictRawRole_AndPrivateResponses(string role,HttpStatusCode expected)
    {
        var s=await Seed(role);using var c=Client(s.Actor,s.Tenant);var response=await c.GetAsync("/api/v1/tenant/calendar/connections");Assert.Equal(expected,response.StatusCode);
        if(expected==HttpStatusCode.OK){Assert.True(response.Headers.CacheControl!.NoStore);Assert.DoesNotContain("ProtectedCredentials",await response.Content.ReadAsStringAsync());}
        using var cross=Client(s.Actor,Guid.NewGuid());Assert.Equal(HttpStatusCode.Forbidden,(await cross.GetAsync("/api/v1/tenant/calendar/connections")).StatusCode);
    }
    [Fact]
    public async Task BookingPermalink_SurvivesProfileRename_AndValidatesTenantSelections()
    {
        var s=await Seed();using var owner=Client(s.Actor,s.Tenant);using var publicClient=Client();
        var created=await owner.PostAsJsonAsync("/api/v1/tenant/booking-links",new BookingLinkRequest(s.Location,s.Service));Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var link=(await created.Content.ReadFromJsonAsync<BookingLinkView>())!;Assert.Contains("Book with Adeni</a>",link.Html);var slug=new Uri(link.Url).AbsolutePath.Split('/')[^1];
        var resolve=$"/api/v1/public/booking-links/{slug}?service={s.Service}";var initial=await publicClient.GetFromJsonAsync<BookingLinkDestination>(resolve);Assert.Contains("/businesses/"+s.Slug,initial!.Path);
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AdeniDbContext>();var location=await db.BusinessLocations.FindAsync(s.Location);location!.Slug="renamed-"+s.Tenant.ToString("N");await db.SaveChangesAsync();}
        var renamed=await publicClient.GetFromJsonAsync<BookingLinkDestination>(resolve);Assert.Contains("/businesses/renamed-",renamed!.Path);
        var other=await Seed();Assert.Equal(HttpStatusCode.BadRequest,(await publicClient.GetAsync($"/api/v1/public/booking-links/{slug}?service={other.Service}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await publicClient.GetAsync("/api/v1/public/booking-links/missing")).StatusCode);
        foreach(var query in new[]{"price=1","tenant="+s.Tenant,"service=invalid",$"service={s.Service}&service={s.Service}","returnUrl=https://evil.test"})Assert.Equal(HttpStatusCode.BadRequest,(await publicClient.GetAsync($"/api/v1/public/booking-links/{slug}?{query}")).StatusCode);
    }
    [Fact]
    public async Task PublicContext_RejectsInactiveCrossBranchAndAddonSelections()
    {
        var s=await Seed();using var c=Client();var staffId=Guid.NewGuid();
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AdeniDbContext>();db.Add(new StaffMember{Id=staffId,TenantId=s.Tenant,DisplayName="Other branch",BusinessLocationId=Guid.NewGuid()});var service=await db.ServiceOfferings.FindAsync(s.Service);service!.IsAddOn=true;await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync($"/api/v1/public/booking-context/{s.Slug}?practitioner={staffId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync($"/api/v1/public/booking-context/{s.Slug}?service={s.Service}")).StatusCode);
        using(var scope=factory.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AdeniDbContext>();var location=await db.BusinessLocations.FindAsync(s.Location);location!.IsActive=false;await db.SaveChangesAsync();}
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/v1/public/booking-context/{s.Slug}")).StatusCode);
    }
    [Fact]
    public async Task OAuth_UnverifiedLaunchCannotRedirectOrSetCorrelationCookie()
    {
        using var c=Client();var response=await c.GetAsync("/api/v1/calendar/oauth/launch?ticket=invalid");Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);Assert.False(response.Headers.Contains("Set-Cookie"));Assert.True(response.Headers.CacheControl!.NoStore);
    }
}
