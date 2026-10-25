namespace Adeni.Infrastructure.Tests.Finance;

using Adeni.Application.Finance;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static CommerceTestSupport;

[Trait("Category", "SqlServer")]
public sealed class CommerceSqlServerTests
{
    private sealed class SqlFactAttribute : FactAttribute
    {
        public SqlFactAttribute() { if(Environment.GetEnvironmentVariable("ADENI_SQLSERVER_TESTS") != "1") Skip="Set ADENI_SQLSERVER_TESTS=1 and publish DACPAC to isolated SQL Server."; }
    }
    private static AdeniDbContext Db(IInterceptor? interceptor=null)
    {
        var builder=new DbContextOptionsBuilder<AdeniDbContext>().UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__AdeniDb"));
        if(interceptor is not null)builder.AddInterceptors(interceptor);
        return new(builder.Options,new TenantContext());
    }
    private sealed class Gate(int participants) : SaveChangesInterceptor
    {
        private int arrived; private readonly TaskCompletionSource release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken ct=default)
        {
            if(Interlocked.Increment(ref arrived)==participants)release.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(20),ct);return result;
        }
    }
    private static async Task Cleanup(Guid tenant, Guid customer)
    {
        await using var db=Db();
        await db.Set<CommerceOperation>().Where(x=>x.TenantId==tenant).ExecuteDeleteAsync();
        await db.Set<CommerceSettlement>().Where(x=>x.TenantId==tenant).ExecuteDeleteAsync();
        await db.Set<GiftCardTransaction>().Where(x=>x.TenantId==tenant && x.ReversesTransactionId!=null).ExecuteDeleteAsync();
        await db.Set<GiftCardTransaction>().Where(x=>x.TenantId==tenant).ExecuteDeleteAsync();
        await db.Set<BusinessGiftCard>().Where(x=>x.TenantId==tenant).ExecuteDeleteAsync();
        await db.Set<CommercePaymentAttempt>().Where(x=>x.TenantId==tenant).ExecuteDeleteAsync();
        await db.Set<CommercePaymentRequest>().Where(x=>x.TenantId==tenant).ExecuteDeleteAsync();
        await db.PaymentIntents.Where(x=>x.TenantId==tenant).ExecuteDeleteAsync();
        await db.Tenants.Where(x=>x.Id==tenant).ExecuteDeleteAsync();await db.Customers.Where(x=>x.Id==customer).ExecuteDeleteAsync();
    }
    [SqlFact]
    public async Task ConcurrentRedemptions_CannotOverspend_AndRetryDoesNotRepeatDelta()
    {
        await using var seed=Db();var s=await Seed(seed);
        try {
            var gift=await Issue(seed,s.Tenant,s.Customer);var service=Service(seed,s.Customer);
            var a=(await service.CreateAsync(s.Tenant,"owner","a",new(70,"NGN","A",AllowsGiftCard:true),default)).Value!;
            var b=(await service.CreateAsync(s.Tenant,"owner","b",new(70,"NGN","B",AllowsGiftCard:true),default)).Value!;
            var gate=new Gate(2);await using var dbA=Db(gate);await using var dbB=Db(gate);
            var results=await Task.WhenAll(Service(dbA,s.Customer).RedeemAsync(Token(a),"redeem",gift.Code,default),Service(dbB,s.Customer).RedeemAsync(Token(b),"redeem",gift.Code,default));
            Assert.Single(results,x=>x.IsSuccess);Assert.Single(results,x=>x.IsFailure);
            await using var check=Db();var card=await check.Set<BusinessGiftCard>().SingleAsync(x=>x.Id==gift.Card.Id);
            Assert.Equal(30,card.RemainingAmount);Assert.Equal(card.RemainingAmount,await check.Set<GiftCardTransaction>().Where(x=>x.GiftCardId==card.Id).SumAsync(x=>x.Delta));
            var winner=results[0].IsSuccess?a:b;
            Assert.True((await Service(check,s.Customer).RedeemAsync(Token(winner),"redeem",gift.Code,default)).IsSuccess);
            Assert.Equal(2,await check.Set<GiftCardTransaction>().CountAsync(x=>x.TenantId==s.Tenant));
        } finally {await Cleanup(s.Tenant,s.Customer);}
    }
    [SqlFact]
    public async Task ConcurrentCompletion_DoesNotDoubleIssueOrSettle()
    {
        await using var seed=Db();var s=await Seed(seed);
        try {
            var service=Service(seed,s.Customer);var r=(await service.CreateAsync(s.Tenant,"owner","purchase",new(100,"NGN","Gift",true),default)).Value!;
            Assert.True((await service.CheckoutAsync(Token(r),"customer","checkout",default)).IsSuccess);
            var id=await seed.Set<CommercePaymentAttempt>().Where(x=>x.RequestId==r.Id).Select(x=>x.PaymentIntentId).SingleAsync();
            var gate=new Gate(2);await using var a=Db(gate);await using var b=Db(gate);
            var ia=await a.PaymentIntents.SingleAsync(x=>x.Id==id);var ib=await b.PaymentIntents.SingleAsync(x=>x.Id==id);
            var payload=new PaymentWebhookPayload("paid",ia.ProviderReference,true,false,false,null,100,"NGN");
            var result=await Task.WhenAll(new CommercePaymentCompletion(a,new Clock()).CompleteAsync(ia,payload,default),new CommercePaymentCompletion(b,new Clock()).CompleteAsync(ib,payload,default));
            Assert.Single(result,x=>x.IsSuccess);
            await using var check=Db();Assert.Single(await check.Set<CommerceSettlement>().Where(x=>x.TenantId==s.Tenant).ToArrayAsync());
            Assert.Single(await check.Set<GiftCardTransaction>().Where(x=>x.TenantId==s.Tenant).ToArrayAsync());
            Assert.Equal(100,(await check.Set<BusinessGiftCard>().SingleAsync(x=>x.TenantId==s.Tenant)).RemainingAmount);
        } finally {await Cleanup(s.Tenant,s.Customer);}
    }
    [SqlFact]
    public async Task CheckoutAndGiftRace_HasOneTenderClaim()
    {
        await using var seed=Db();var s=await Seed(seed);
        try {
            var gift=await Issue(seed,s.Tenant,s.Customer);var r=(await Service(seed,s.Customer).CreateAsync(s.Tenant,"owner","charge",new(10,"NGN","Charge",AllowsGiftCard:true),default)).Value!;
            var gate=new Gate(2);await using var a=Db(gate);await using var b=Db(gate);
            var checkout=Service(a,s.Customer).CheckoutAsync(Token(r),null,"checkout",default);var redeem=Service(b,s.Customer).RedeemAsync(Token(r),"redeem",gift.Code,default);
            await Task.WhenAll(checkout,redeem);Assert.NotEqual(checkout.Result.IsSuccess,redeem.Result.IsSuccess);
            await using var check=Db();var row=await check.Set<CommercePaymentRequest>().SingleAsync(x=>x.Id==r.Id);
            Assert.Contains(row.State,new[]{PaymentRequestState.Paid,PaymentRequestState.PaymentPending});
            Assert.Equal(checkout.Result.IsSuccess?100:90,(await check.Set<BusinessGiftCard>().SingleAsync(x=>x.Id==gift.Card.Id)).RemainingAmount);
        } finally {await Cleanup(s.Tenant,s.Customer);}
    }
    [SqlFact]
    public async Task CancelAndCheckoutRace_CannotCancelAnAdmittedPayment()
    {
        await using var seed=Db();var s=await Seed(seed);
        try {
            var r=(await Service(seed,s.Customer).CreateAsync(s.Tenant,"owner","charge",new(10,"NGN","Charge"),default)).Value!;
            var gate=new Gate(2);await using var a=Db(gate);await using var b=Db(gate);
            var checkout=Service(a,s.Customer).CheckoutAsync(Token(r),null,"checkout",default);var cancel=Service(b,s.Customer).CancelAsync(s.Tenant,"owner",r.Id,"cancel",default);
            await Task.WhenAll(checkout,cancel);Assert.NotEqual(checkout.Result.IsSuccess,cancel.Result.IsSuccess);
            await using var check=Db();var row=await check.Set<CommercePaymentRequest>().SingleAsync(x=>x.Id==r.Id);
            Assert.Equal(checkout.Result.IsSuccess?PaymentRequestState.PaymentPending:PaymentRequestState.Cancelled,row.State);
        } finally {await Cleanup(s.Tenant,s.Customer);}
    }
    [SqlFact]
    public async Task CrossTenantLedgerForeignKeyAndNegativeBalanceAreRejected()
    {
        await using var seed=Db();var s=await Seed(seed);var other=await Seed(seed);
        try {
            var gift=await Issue(seed,s.Tenant,s.Customer);
            await using(var queries=Db()){
                var metadata=await new CustomerGiftCardQuery(queries,new Access(s.Customer)).GetPurchasedAsync(new(s.Tenant,Guid.NewGuid(),s.Customer,null),"owner",default);
                Assert.Single(metadata.Value!);
                var report=await new GiftCardReportQuery(queries,new Access(s.Customer)).GetAsync(s.Tenant,"owner",Now.AddDays(-1),Now.AddDays(1),default);
                Assert.Equal(100,Assert.Single(report.Value!).OutstandingValue);
            }
            await using(var db=Db()){
                db.Add(new GiftCardTransaction{Id=Guid.NewGuid(),TenantId=other.Tenant,GiftCardId=gift.Card.Id,RequestId=gift.Request.Id,Sequence=99,Kind=GiftTransactionKind.Redeem,Delta=-1,BalanceAfter=99,Actor="holder",OccurredAt=Now});
                await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
            }
            await using(var db=Db()){
                var card=await db.Set<BusinessGiftCard>().SingleAsync(x=>x.Id==gift.Card.Id);card.RemainingAmount=-1;
                await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
            }
        } finally {await Cleanup(s.Tenant,s.Customer);await Cleanup(other.Tenant,other.Customer);}
    }

    [SqlFact]
    public async Task ConcurrentSameRequestRetryAndReversal_AreLedgerIdempotent()
    {
        await using var seed=Db();var s=await Seed(seed);
        try {
            var gift=await Issue(seed,s.Tenant,s.Customer);var request=(await Service(seed,s.Customer).CreateAsync(s.Tenant,"owner","charge",new(20,"NGN","Charge",AllowsGiftCard:true),default)).Value!;
            var gate=new Gate(2);await using(var a=Db(gate))await using(var b=Db(gate)){
                var result=await Task.WhenAll(Service(a,s.Customer).RedeemAsync(Token(request),"same",gift.Code,default),Service(b,s.Customer).RedeemAsync(Token(request),"same",gift.Code,default));
                Assert.Single(result,x=>x.IsSuccess);
            }
            await using(var retry=Db())Assert.True((await Service(retry,s.Customer).RedeemAsync(Token(request),"same",gift.Code,default)).IsSuccess);
            var reverseGate=new Gate(2);await using(var a=Db(reverseGate))await using(var b=Db(reverseGate)){
                var result=await Task.WhenAll(Service(a,s.Customer).ReverseAsync(s.Tenant,"owner",request.Id,"reverse",default),Service(b,s.Customer).ReverseAsync(s.Tenant,"owner",request.Id,"reverse",default));
                Assert.Single(result,x=>x.IsSuccess);
            }
            await using var check=Db();var card=await check.Set<BusinessGiftCard>().SingleAsync(x=>x.Id==gift.Card.Id);
            Assert.Equal(100,card.RemainingAmount);Assert.Equal(3,await check.Set<GiftCardTransaction>().CountAsync(x=>x.GiftCardId==card.Id));
        } finally {await Cleanup(s.Tenant,s.Customer);}
    }
    [SqlFact]
    public async Task FailedRelationalSave_RollsBackDebitSettlementAndOperationReceipt()
    {
        await using var seed=Db();var s=await Seed(seed);
        try {
            var gift=await Issue(seed,s.Tenant,s.Customer);var r=(await Service(seed,s.Customer).CreateAsync(s.Tenant,"owner","charge",new(10,"NGN","Charge",AllowsGiftCard:true),default)).Value!;
            await using(var fail=Db()){
                fail.Add(new GiftCardTransaction{Id=Guid.NewGuid(),TenantId=s.Tenant,GiftCardId=Guid.NewGuid(),RequestId=r.Id,Sequence=1,Kind=GiftTransactionKind.Redeem,Delta=-1,BalanceAfter=0,Actor="test",OccurredAt=Now});
                Assert.True((await Service(fail,s.Customer).RedeemAsync(Token(r),"redeem",gift.Code,default)).IsFailure);
            }
            await using var check=Db();Assert.Equal(100,(await check.Set<BusinessGiftCard>().SingleAsync(x=>x.Id==gift.Card.Id)).RemainingAmount);
            Assert.False(await check.Set<CommerceSettlement>().AnyAsync(x=>x.RequestId==r.Id));Assert.False(await check.Set<CommerceOperation>().AnyAsync(x=>x.RequestId==r.Id&&x.Kind=="redeemed"));
            Assert.Equal(PaymentRequestState.Active,(await check.Set<CommercePaymentRequest>().SingleAsync(x=>x.Id==r.Id)).State);
        } finally {await Cleanup(s.Tenant,s.Customer);}
    }

    [SqlFact]
    public async Task UnusedPurchaseRefundAndRedemptionRace_CannotSpendRefundedValue()
    {
        await using var seed=Db();var s=await Seed(seed);
        try {
            var gift=await Issue(seed,s.Tenant,s.Customer);var request=(await Service(seed,s.Customer).CreateAsync(s.Tenant,"owner","charge",new(10,"NGN","Charge",AllowsGiftCard:true),default)).Value!;
            var gate=new Gate(2);await using var a=Db(gate);await using var b=Db(gate);
            var refund=Service(a,s.Customer).RefundAsync(s.Tenant,"owner",gift.Request.Id,"refund",default);
            var redeem=Service(b,s.Customer).RedeemAsync(Token(request),"redeem",gift.Code,default);
            await Task.WhenAll(refund,redeem);Assert.NotEqual(refund.Result.IsSuccess,redeem.Result.IsSuccess);
            await using var check=Db();var card=await check.Set<BusinessGiftCard>().SingleAsync(x=>x.Id==gift.Card.Id);
            Assert.Equal(refund.Result.IsSuccess?0:90,card.RemainingAmount);
            Assert.Equal(card.RemainingAmount,await check.Set<GiftCardTransaction>().Where(x=>x.GiftCardId==card.Id).SumAsync(x=>x.Delta));
        } finally {await Cleanup(s.Tenant,s.Customer);}
    }
}
