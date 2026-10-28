namespace Adeni.Application.Tests.Tenancy;
using Adeni.Application.Finance;
using Adeni.Application.Subscriptions;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Domain.Subscriptions;
public sealed class CustomerGiftCardsQueryServiceTests
{
    private sealed class Profiles(Result<CustomerProfileResponse> result):ICustomerProfileQueryService
    {
        public Task<Result<CustomerProfileResponse>> GetProfileAsync(Guid t,Guid c,string a,Guid? l=null,CancellationToken cancellationToken=default)=>Task.FromResult(result);
        public Task<Result<CustomerActivityResponse>> GetActivityAsync(Guid t,Guid c,string a,Guid? l=null,int pageSize=25,string? cursor=null,CancellationToken cancellationToken=default)=>throw new NotSupportedException();
    }
    private sealed class Contexts(Guid customer):ICustomerProfileContextQuery
    {
        public Task<Result<CustomerProfileContext>> GetAsync(Guid tenant,Guid roster,CancellationToken ct)=>Task.FromResult(Result.Success(new CustomerProfileContext(roster,customer,System.Text.Json.JsonSerializer.Deserialize<TenantEntitlements>("{}")!)));
    }
    private sealed class Gifts:ICustomerGiftCardQuery
    {
        public CustomerProfileScope? Scope;public int Calls;
        public Task<Result<IReadOnlyList<GiftCardView>>> GetPurchasedAsync(CustomerProfileScope scope,string actor,CancellationToken ct){Calls++;Scope=scope;return Task.FromResult(Result.Success<IReadOnlyList<GiftCardView>>([]));}
    }
    private static CustomerProfileResponse Profile(Guid roster,string finance="available",Guid? location=null)=>new(roster,DateTimeOffset.UtcNow,location,new("available",finance,"restricted"),null,null,null);
    [Fact]
    public async Task ExistingCustomerAccessDenial_NeverInvokesFinanceGiftSource()
    {
        var gifts=new Gifts();var service=new CustomerGiftCardsQueryService(new Profiles(Result.Failure<CustomerProfileResponse>(Error.Forbidden("Denied"))),new Contexts(Guid.NewGuid()),gifts);
        Assert.True((await service.GetAsync(Guid.NewGuid(),Guid.NewGuid(),"actor",null,default)).IsFailure);Assert.Equal(0,gifts.Calls);
    }
    [Theory][InlineData("restricted")][InlineData("notEntitled")][InlineData("notLinked")]
    public async Task UnavailableFinanceSource_DoesNotLeakGiftMetadata(string availability)
    {
        var gifts=new Gifts();var roster=Guid.NewGuid();var service=new CustomerGiftCardsQueryService(new Profiles(Result.Success(Profile(roster,availability))),new Contexts(Guid.NewGuid()),gifts);
        Assert.True((await service.GetAsync(Guid.NewGuid(),roster,"actor",null,default)).IsFailure);Assert.Equal(0,gifts.Calls);
    }
    [Fact]
    public async Task LocationScope_CannotExpandToTenantWidePurchasedValue()
    {
        var gifts=new Gifts();var roster=Guid.NewGuid();var location=Guid.NewGuid();var service=new CustomerGiftCardsQueryService(new Profiles(Result.Success(Profile(roster,location:location))),new Contexts(Guid.NewGuid()),gifts);
        Assert.Equal("validation",(await service.GetAsync(Guid.NewGuid(),roster,"actor",location,default)).Error.Code);Assert.Equal(0,gifts.Calls);
    }
    [Fact]
    public async Task AuthorizedRead_UsesResolvedPlatformCustomerAndOriginalTenantScope()
    {
        var gifts=new Gifts();var roster=Guid.NewGuid();var customer=Guid.NewGuid();var tenant=Guid.NewGuid();
        var service=new CustomerGiftCardsQueryService(new Profiles(Result.Success(Profile(roster))),new Contexts(customer),gifts);
        Assert.True((await service.GetAsync(tenant,roster,"actor",null,default)).IsSuccess);Assert.Equal(new CustomerProfileScope(tenant,roster,customer,null),gifts.Scope);Assert.Equal(1,gifts.Calls);
    }
}
