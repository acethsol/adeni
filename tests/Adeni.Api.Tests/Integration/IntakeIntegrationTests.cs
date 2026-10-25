namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Application.Booking;
using Adeni.Application.Intake;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Intake;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class IntakeIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    public IntakeIntegrationTests(WebApplicationFactory<Program> factory) => this.factory = factory.WithWebHostBuilder(b =>
    {
        b.UseEnvironment("Testing"); b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?> {
            ["ConnectionStrings:AdeniDb"]="", ["Redis:ConnectionString"]="", ["Auth0:Enabled"]="false" }));
    });
    private async Task<(Guid Tenant, Guid Booking, Guid Service, string Owner, string Customer)> Seed(string role="owner")
    {
        var tenant=Guid.NewGuid();var customer=Guid.NewGuid();var service=Guid.NewGuid();var booking=Guid.NewGuid();var owner=$"owner-{tenant:N}";var sub=$"customer-{customer:N}";
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.Add(new Tenant{Id=tenant,Name="Intake tests",Status=TenantStatus.Verified});
        db.Add(new BusinessUser{Id=Guid.NewGuid(),TenantId=tenant,Auth0Sub=owner,Role=role});
        db.Add(new Customer{Id=customer,Auth0Sub=sub,Name="Ada"});
        db.Add(new ServiceOffering{Id=service,TenantId=tenant,Name="Hair care",Currency="NGN",DurationMinutes=30});
        db.Add(new BookingRecord{Id=booking,TenantId=tenant,CustomerId=customer,ServiceOfferingId=service,StartAt=DateTimeOffset.UtcNow.AddDays(7),EndAt=DateTimeOffset.UtcNow.AddDays(7).AddHours(1),Status=BookingStatus.Pending});
        await db.SaveChangesAsync();return(tenant,booking,service,owner,sub);
    }
    private HttpClient Client(string sub, Guid? tenant=null)
    {
        var client=factory.CreateClient();client.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub",sub);
        if(tenant is Guid id)client.DefaultRequestHeaders.Add("X-Tenant-Id",id.ToString());return client;
    }
    private async Task<(Guid Template,Guid Version,Guid Field,Guid Assignment)> Assign(Guid tenant,Guid booking,Guid service,string owner)
    {
        using var client=Client(owner,tenant);
        var created=await client.PostAsJsonAsync("/api/v1/tenant/intake/templates",new {name="Consultation"});Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var detail=(await created.Content.ReadFromJsonAsync<IntakeTemplateDetail>())!;var field=Guid.NewGuid();
        var definition=new IntakeDefinition("Original title",null,"Original instructions",[new(field,IntakeFieldType.Acknowledgement,"Exact original acknowledgement",null,true,[])]);
        var save=await client.PutAsJsonAsync($"/api/v1/tenant/intake/templates/{detail.Id}/draft",new {detail.Revision,definition});Assert.Equal(HttpStatusCode.OK,save.StatusCode);
        detail=(await save.Content.ReadFromJsonAsync<IntakeTemplateDetail>())!;
        var publish=await client.PostAsJsonAsync($"/api/v1/tenant/intake/templates/{detail.Id}/publish",new{detail.Revision});Assert.Equal(HttpStatusCode.OK,publish.StatusCode);
        detail=(await publish.Content.ReadFromJsonAsync<IntakeTemplateDetail>())!;
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync($"/api/v1/tenant/intake/services/{service}",new{templateId=detail.Id,revision=""})).StatusCode);
        using var scope=factory.Services.CreateScope();var writer=scope.ServiceProvider.GetRequiredService<IBookingIntakeAssignmentWriter>();
        var required=await writer.PrepareAsync(tenant,[service,service],default);Assert.Single(required.Value!);
        writer.Stage(tenant,booking,required.Value!,DateTimeOffset.UtcNow);var db=scope.ServiceProvider.GetRequiredService<AdeniDbContext>();await db.SaveChangesAsync();
        var assignment=await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Set<BookingIntakeAssignment>().Where(x=>x.TenantId==tenant&&x.BookingId==booking));
        return(detail.Id,detail.PublishedVersionId!.Value,field,assignment.Id);
    }
    [Theory]
    [InlineData("owner",HttpStatusCode.OK)] [InlineData("manager",HttpStatusCode.OK)]
    [InlineData("receptionist",HttpStatusCode.Forbidden)] [InlineData("practitioner",HttpStatusCode.Forbidden)]
    [InlineData("ops",HttpStatusCode.Forbidden)] [InlineData("accountant",HttpStatusCode.Forbidden)] [InlineData("unknown",HttpStatusCode.Forbidden)]
    public async Task Management_UsesExplicitRoles(string role,HttpStatusCode expected)
    {
        var seed=await Seed(role);using var client=Client(seed.Owner,seed.Tenant);
        Assert.Equal(expected,(await client.GetAsync("/api/v1/tenant/intake/templates")).StatusCode);
    }
    [Fact]
    public async Task CustomerSubmission_IsOwnedFinalIdempotentNoStore_AndKeepsOriginalVersion()
    {
        var s=await Seed();var form=await Assign(s.Tenant,s.Booking,s.Service,s.Owner);
        using var customer=Client(s.Customer);var path=$"/api/v1/bookings/{s.Booking}/intake/{form.Assignment}";
        var get=await customer.GetAsync(path);Assert.Equal(HttpStatusCode.OK,get.StatusCode);Assert.True(get.Headers.CacheControl!.NoStore);
        var view=(await get.Content.ReadFromJsonAsync<IntakeSubmissionView>())!;Assert.Equal(form.Version,view.Assignment.VersionId);
        using(var owner=Client(s.Owner,s.Tenant)){
            var detail=(await owner.GetFromJsonAsync<IntakeTemplateDetail>($"/api/v1/tenant/intake/templates/{form.Template}"))!;
            var clone=await owner.PostAsJsonAsync($"/api/v1/tenant/intake/templates/{form.Template}/draft",new{detail.Revision});
            Assert.Equal(HttpStatusCode.OK,clone.StatusCode);detail=(await clone.Content.ReadFromJsonAsync<IntakeTemplateDetail>())!;
            var changed=detail.Draft! with {Title="New version title"};
            var saved=await owner.PutAsJsonAsync($"/api/v1/tenant/intake/templates/{form.Template}/draft",new {detail.Revision,definition=changed});
            detail=(await saved.Content.ReadFromJsonAsync<IntakeTemplateDetail>())!;
            Assert.Equal(HttpStatusCode.OK,(await owner.PostAsJsonAsync($"/api/v1/tenant/intake/templates/{form.Template}/publish",new{detail.Revision})).StatusCode);
        }
        customer.DefaultRequestHeaders.Add("Idempotency-Key",Guid.NewGuid().ToString());
        var body=new SubmitIntakeRequest(form.Version,[new(form.Field,Boolean:true)]);
        var result=await customer.PostAsJsonAsync(path+"/submission",body);Assert.Equal(HttpStatusCode.OK,result.StatusCode);
        Assert.Equal("completed",(await result.Content.ReadFromJsonAsync<IntakeAssignmentStatus>())!.State);
        Assert.Equal(HttpStatusCode.OK,(await customer.PostAsJsonAsync(path+"/submission",body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await customer.PostAsJsonAsync(path+"/submission",body with{Answers=[new(form.Field,Boolean:false)]})).StatusCode);
        customer.DefaultRequestHeaders.Remove("Idempotency-Key");customer.DefaultRequestHeaders.Add("Idempotency-Key","changed-key");
        Assert.Equal(HttpStatusCode.Conflict,(await customer.PostAsJsonAsync(path+"/submission",body)).StatusCode);
        var completed=(await customer.GetFromJsonAsync<IntakeSubmissionView>(path))!;
        Assert.Equal("Exact original acknowledgement",completed.Definition.Fields[0].Label);Assert.True(completed.Answers![0].Boolean);
    }
    [Fact]
    public async Task CustomerAndStaffCannotReadAnotherCustomersOrTenantsAnswers()
    {
        var a=await Seed();var b=await Seed("receptionist");var form=await Assign(a.Tenant,a.Booking,a.Service,a.Owner);
        using var other=Client(b.Customer);
        Assert.Equal(HttpStatusCode.NotFound,(await other.GetAsync($"/api/v1/bookings/{a.Booking}/intake/{form.Assignment}")).StatusCode);
        using var otherTenant=Client(b.Owner,b.Tenant);
        Assert.NotEqual(HttpStatusCode.OK,(await otherTenant.GetAsync($"/api/v1/tenant/intake/bookings/{a.Booking}/forms/{form.Assignment}/submission")).StatusCode);
        using(var scope=factory.Services.CreateScope()){
            var db=scope.ServiceProvider.GetRequiredService<AdeniDbContext>();db.Add(new BusinessUser{Id=Guid.NewGuid(),TenantId=a.Tenant,Auth0Sub="receptionist-"+a.Tenant,Role="receptionist"});await db.SaveChangesAsync();}
        using var receptionist=Client("receptionist-"+a.Tenant,a.Tenant);
        Assert.Equal(HttpStatusCode.OK,(await receptionist.GetAsync($"/api/v1/tenant/intake/bookings/{a.Booking}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await receptionist.GetAsync($"/api/v1/tenant/intake/bookings/{a.Booking}/forms/{form.Assignment}/submission")).StatusCode);
    }
    [Fact]
    public async Task InvalidBodyAndCrossVersion_DoNotEchoSensitiveValues()
    {
        var s=await Seed();var form=await Assign(s.Tenant,s.Booking,s.Service,s.Owner);using var client=Client(s.Customer);
        client.DefaultRequestHeaders.Add("Idempotency-Key","safe-retry");var path=$"/api/v1/bookings/{s.Booking}/intake/{form.Assignment}/submission";
        var response=await client.PostAsJsonAsync(path,new{versionId=form.Version,answers=new[]{new{fieldId=form.Field,boolean="sensitive-sentinel"}}});
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);Assert.DoesNotContain("sensitive-sentinel",await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(path,new SubmitIntakeRequest(Guid.NewGuid(),[new(form.Field,Boolean:true)]))).StatusCode);
    }
}
