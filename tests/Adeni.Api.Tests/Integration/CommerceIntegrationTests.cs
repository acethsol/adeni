namespace Adeni.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using Adeni.Application.Finance;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public sealed class CommerceIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    public CommerceIntegrationTests(WebApplicationFactory<Program> factory) => this.factory=factory.WithWebHostBuilder(b=>{
        b.UseEnvironment("Testing");b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{
            ["ConnectionStrings:AdeniDb"]="",["Redis:ConnectionString"]="",["Auth0:Enabled"]="false",["Finance:Provider"]="Stub"}));
    });
    private async Task<(Guid Tenant,string Actor,string Customer)> Seed(string role="owner")
    {
        var tenant=Guid.NewGuid();var actor=$"finance-{tenant:N}";var customer=$"payer-{tenant:N}";
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AdeniDbContext>();
        db.Add(new Tenant{Id=tenant,Name="Tenant-specific gift business",Status=TenantStatus.Verified});
        db.Add(new BusinessUser{Id=Guid.NewGuid(),TenantId=tenant,Auth0Sub=actor,Role=role});
        db.Add(new Customer{Id=Guid.NewGuid(),Auth0Sub=customer,Name="Purchaser"});await db.SaveChangesAsync();return(tenant,actor,customer);
    }
    private HttpClient Client(string? actor=null,Guid? tenant=null,string? token=null)
    {
        var c=factory.CreateClient();if(actor!=null)c.DefaultRequestHeaders.Add("X-Dev-Auth0-Sub",actor);
        if(tenant!=null)c.DefaultRequestHeaders.Add("X-Tenant-Id",tenant.ToString());
        if(token!=null)c.DefaultRequestHeaders.Add("X-Adeni-Pay-Token",token);c.DefaultRequestHeaders.Add("Idempotency-Key",Guid.NewGuid().ToString());return c;
    }
    [Theory][InlineData("owner",HttpStatusCode.OK)][InlineData("manager",HttpStatusCode.OK)]
    [InlineData("accountant",HttpStatusCode.Forbidden)][InlineData("receptionist",HttpStatusCode.Forbidden)]
    [InlineData("practitioner",HttpStatusCode.Forbidden)][InlineData("unknown",HttpStatusCode.Forbidden)]
    public async Task Creation_UsesRawPersistedFinanceRole(string role,HttpStatusCode expected)
    {
        var s=await Seed(role);using var c=Client(s.Actor,s.Tenant);
        Assert.Equal(expected,(await c.PostAsJsonAsync("/api/v1/tenant/finance/pay-links",new CreateCommerceRequest(100,"NGN","Charge"))).StatusCode);
    }
    [Fact]
    public async Task GiftFlow_ConfirmsThenRevealsToPurchaser_AndSettlesTenantCharge()
    {
        var s=await Seed();using var owner=Client(s.Actor,s.Tenant);
        var created=await owner.PostAsJsonAsync("/api/v1/tenant/finance/pay-links",new CreateCommerceRequest(100,"NGN","Gift",true));Assert.Equal(HttpStatusCode.OK,created.StatusCode);
        var gift=(await created.Content.ReadFromJsonAsync<CommerceRequestView>())!;var token=gift.ShareUrl.Split("#t=")[1];
        using var anon=Client(token:token);var read=await anon.GetAsync("/api/v1/public/pay-links/resolve");
        Assert.True(read.Headers.CacheControl!.NoStore);Assert.Contains("noindex",string.Join(",",read.Headers.GetValues("X-Robots-Tag")));
        var json=await read.Content.ReadAsStringAsync();Assert.DoesNotContain(s.Tenant.ToString(),json);Assert.DoesNotContain("paymentIntentId",json);
        Assert.Equal(HttpStatusCode.Forbidden,(await anon.PostAsync("/api/v1/public/pay-links/checkout",null)).StatusCode);
        using var customer=Client(s.Customer,token:token);
        var checkout=await customer.PostAsync("/api/v1/public/pay-links/checkout",null);Assert.Equal(HttpStatusCode.OK,checkout.StatusCode);
        var url=(await checkout.Content.ReadFromJsonAsync<CommerceCheckoutView>())!.CheckoutUrl!;var reference=url.Split('/')[^1];
        Assert.Empty((await customer.GetFromJsonAsync<GiftCardView[]>("/api/v1/me/gift-cards"))!);
        Assert.Equal(HttpStatusCode.OK,(await anon.PostAsJsonAsync("/api/v1/finance/stub/confirm",new{reference})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await anon.PostAsJsonAsync("/api/v1/finance/stub/confirm",new{reference})).StatusCode);
        using(var ledger=System.Text.Json.JsonDocument.Parse(await owner.GetStringAsync($"/api/v1/finance/ledger?tenantId={s.Tenant}"))){
            var paymentId=ledger.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.Conflict,(await owner.PostAsJsonAsync($"/api/v1/finance/{paymentId}/refund",new RefundPaymentRequest(s.Tenant,paymentId))).StatusCode);
        }
        var from=Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));var to=Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var export=await owner.GetAsync($"/api/v1/tenant/reporting/exports/gift-cards?from={from}&to={to}&format=csv");Assert.Equal(HttpStatusCode.OK,export.StatusCode);
        Assert.Contains("OutstandingValue",await export.Content.ReadAsStringAsync());
        var card=Assert.Single((await customer.GetFromJsonAsync<GiftCardView[]>("/api/v1/me/gift-cards"))!);
        var revealed=await customer.PostAsync($"/api/v1/me/gift-cards/{card.Id}/reveal",null);Assert.Equal(HttpStatusCode.OK,revealed.StatusCode);
        var code=(await revealed.Content.ReadFromJsonAsync<GiftCodeView>())!.Code;
        Assert.Equal(HttpStatusCode.NotFound,(await owner.PostAsync($"/api/v1/me/gift-cards/{card.Id}/reveal",null)).StatusCode);
        owner.DefaultRequestHeaders.Remove("Idempotency-Key");owner.DefaultRequestHeaders.Add("Idempotency-Key","charge");
        var charge=(await (await owner.PostAsJsonAsync("/api/v1/tenant/finance/pay-links",new CreateCommerceRequest(40,"NGN","Walk-in",AllowsGiftCard:true))).Content.ReadFromJsonAsync<CommerceRequestView>())!;
        using var payer=Client(token:charge.ShareUrl.Split("#t=")[1]);
        var paid=await payer.PostAsJsonAsync("/api/v1/public/pay-links/redeem",new RedeemCommerceRequest(code));Assert.Equal(HttpStatusCode.OK,paid.StatusCode);
        Assert.Equal("Paid",(await paid.Content.ReadFromJsonAsync<PublicCommerceView>())!.State);
        Assert.Equal(HttpStatusCode.OK,(await payer.PostAsJsonAsync("/api/v1/public/pay-links/redeem",new RedeemCommerceRequest(code))).StatusCode);
        Assert.Equal(60,Assert.Single((await customer.GetFromJsonAsync<GiftCardView[]>("/api/v1/me/gift-cards"))!).RemainingAmount);
        Assert.DoesNotContain(code,await (await owner.GetAsync("/api/v1/tenant/finance/gift-cards")).Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task CrossTenantAndMalformedRequests_AreDeniedWithoutCredentialEcho()
    {
        var a=await Seed();var b=await Seed();using var other=Client(a.Actor,b.Tenant);
        Assert.Equal(HttpStatusCode.Forbidden,(await other.GetAsync("/api/v1/tenant/finance/pay-links")).StatusCode);
        using var anon=Client(token:"sensitive-invalid-sentinel");var response=await anon.GetAsync("/api/v1/public/pay-links/resolve");
        Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);Assert.DoesNotContain("sensitive-invalid-sentinel",await response.Content.ReadAsStringAsync());
        using var owner=Client(a.Actor,a.Tenant);owner.DefaultRequestHeaders.Remove("Idempotency-Key");
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsJsonAsync("/api/v1/tenant/finance/pay-links",new CreateCommerceRequest(10,"NGN","Charge"))).StatusCode);
    }
}
