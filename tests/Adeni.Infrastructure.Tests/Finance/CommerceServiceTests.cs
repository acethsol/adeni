namespace Adeni.Infrastructure.Tests.Finance;

using Adeni.Application.Finance;
using Adeni.Domain.Finance;
using Adeni.Infrastructure.Finance;
using Microsoft.EntityFrameworkCore;
using static CommerceTestSupport;

public sealed class CommerceServiceTests
{
    [Theory]
    [InlineData(0, "NGN")][InlineData(-1, "NGN")][InlineData(1.001, "NGN")][InlineData(10000000000, "NGN")][InlineData(1, "JPY")]
    public async Task Create_RejectsInvalidMoney(decimal amount, string currency)
    {
        await using var db = Memory(); var s = await Seed(db);
        Assert.Equal("commerce.validation", (await Service(db,s.Customer).CreateAsync(s.Tenant,"owner","key",new(amount,currency,"Charge"),default)).Error.Code);
    }
    [Fact]
    public async Task Create_ScopesIdempotencyAndRejectsChangedSemanticPayload()
    {
        await using var db=Memory();var s=await Seed(db);var service=Service(db,s.Customer);
        var a=await service.CreateAsync(s.Tenant,"owner","create",new(10,"ngn","Charge"),default);
        var b=await service.CreateAsync(s.Tenant,"owner","create",new(10,"NGN","Charge"),default);
        Assert.Equal(a.Value!.Id,b.Value!.Id);
        Assert.Equal("commerce.conflict",(await service.CreateAsync(s.Tenant,"owner","create",new(11,"NGN","Charge"),default)).Error.Code);
        Assert.True((await service.CreateAsync(s.Tenant,"manager","create",new(11,"NGN","Charge"),default)).IsSuccess);
    }
    [Theory][InlineData("unknown")][InlineData("accountant")][InlineData("receptionist")][InlineData("practitioner")]
    public async Task Create_DeniesNonManagementRoles(string actor)
    {
        await using var db=Memory();var s=await Seed(db);
        Assert.Equal("forbidden",(await Service(db,s.Customer).CreateAsync(s.Tenant,actor,"key",new(10,"NGN","Charge"),default)).Error.Code);
    }
    [Theory][InlineData("Production")][InlineData("Staging")]
    public async Task Create_BlocksLiveWithoutMerchantSettlement(string env)
    {
        await using var db=Memory();var s=await Seed(db);
        Assert.Equal("commerce.settlement_required",(await Service(db,s.Customer,env).CreateAsync(s.Tenant,"owner","key",new(10,"NGN","Charge"),default)).Error.Code);
    }
    [Fact]
    public async Task Redemption_IsFinalIdempotentLedgerBackedAndReversesExactlyOnce()
    {
        await using var db=Memory();var s=await Seed(db);var issued=await Issue(db,s.Tenant,s.Customer);var service=Service(db,s.Customer);
        var r=(await service.CreateAsync(s.Tenant,"owner","charge",new(40,"NGN","Charge",AllowsGiftCard:true),default)).Value!;
        Assert.True((await service.RedeemAsync(Token(r),"redeem",issued.Code,default)).IsSuccess);
        Assert.True((await service.RedeemAsync(Token(r),"redeem",issued.Code,default)).IsSuccess);
        Assert.Equal(60,issued.Card.RemainingAmount);Assert.Equal(2,await db.Set<GiftCardTransaction>().CountAsync());
        Assert.Equal("commerce.conflict",(await service.RedeemAsync(Token(r),"different",issued.Code,default)).Error.Code);
        Assert.True((await service.ReverseAsync(s.Tenant,"owner",r.Id,"reverse",default)).IsSuccess);
        Assert.True((await service.ReverseAsync(s.Tenant,"owner",r.Id,"reverse",default)).IsSuccess);
        Assert.Equal(100,issued.Card.RemainingAmount);Assert.Equal(3,await db.Set<GiftCardTransaction>().CountAsync());
        Assert.Equal("commerce.conflict",(await service.ReverseAsync(s.Tenant,"owner",r.Id,"new",default)).Error.Code);
        Assert.Equal(100,await db.Set<GiftCardTransaction>().SumAsync(x=>x.Delta));
    }
    [Fact]
    public async Task Redemption_RejectsCrossTenantCurrencyInsufficientAndGiftPurchase()
    {
        await using var db=Memory();var a=await Seed(db);var b=await Seed(db);var gift=await Issue(db,a.Tenant,a.Customer);var service=Service(db,a.Customer);
        foreach(var body in new[]{new CreateCommerceRequest(101,"NGN","Too much",AllowsGiftCard:true),new(10,"CAD","Wrong currency",AllowsGiftCard:true)})
        {
            var r=(await service.CreateAsync(a.Tenant,"owner",Guid.NewGuid().ToString(),body,default)).Value!;
            Assert.Equal("commerce.credential_invalid",(await service.RedeemAsync(Token(r),"key",gift.Code,default)).Error.Code);
        }
        var other=(await service.CreateAsync(b.Tenant,"owner","other",new(10,"NGN","Other",AllowsGiftCard:true),default)).Value!;
        Assert.Equal("commerce.credential_invalid",(await service.RedeemAsync(Token(other),"key",gift.Code,default)).Error.Code);
        Assert.Equal("commerce.validation",(await service.CreateAsync(a.Tenant,"owner","invalid",new(10,"NGN","Purchase",true,true),default)).Error.Code);
    }
    [Fact]
    public async Task Completion_ValidatesMoneyAndDoesNotDoubleIssue()
    {
        await using var db=Memory();var s=await Seed(db);var gift=await Issue(db,s.Tenant,s.Customer);
        var intent=await db.PaymentIntents.SingleAsync();var completion=new CommercePaymentCompletion(db,new Clock());
        Assert.True((await completion.CompleteAsync(intent,new("paid",intent.ProviderReference,true,false,false,null,100,"NGN"),default)).IsSuccess);
        Assert.Equal("commerce.validation",(await completion.CompleteAsync(intent,new("paid",intent.ProviderReference,true,false,false,null,99,"NGN"),default)).Error.Code);
        Assert.Single(await db.Set<GiftCardTransaction>().ToArrayAsync());
        Assert.DoesNotContain(gift.Code,gift.Card.ToString());Assert.DoesNotContain(gift.Code,new RedeemCommerceRequest(gift.Code).ToString());
    }
    [Fact]
    public async Task Checkout_ReservesTenderAndCancellationCannotDiscardPendingMoney()
    {
        await using var db=Memory();var s=await Seed(db);var gift=await Issue(db,s.Tenant,s.Customer);var service=Service(db,s.Customer);
        var r=(await service.CreateAsync(s.Tenant,"owner","charge",new(10,"NGN","Charge",AllowsGiftCard:true),default)).Value!;
        Assert.True((await service.CheckoutAsync(Token(r),null,"checkout",default)).IsSuccess);
        Assert.True((await service.CheckoutAsync(Token(r),null,"checkout",default)).IsSuccess);
        Assert.Equal("commerce.conflict",(await service.CancelAsync(s.Tenant,"owner",r.Id,"cancel",default)).Error.Code);
        Assert.Equal("commerce.conflict",(await service.RedeemAsync(Token(r),"redeem",gift.Code,default)).Error.Code);
        Assert.True((await service.CheckoutAsync(Token(r),null,"reopened-browser",default)).IsSuccess);
        Assert.Equal(2,await db.PaymentIntents.CountAsync());
    }
    [Fact]
    public async Task Privacy_DetachesPurchaserWithoutDestroyingLedgerOrValue()
    {
        await using var db=Memory();var s=await Seed(db);var gift=await Issue(db,s.Tenant,s.Customer);
        var privacy=new CommercePrivacyService(db);var exported=await privacy.ExportAsync(s.Customer,default);
        Assert.Single(exported.PurchasedCards);Assert.DoesNotContain(gift.Code,System.Text.Json.JsonSerializer.Serialize(exported));
        await privacy.StageErasureAsync(s.Customer,default);await db.SaveChangesAsync();
        Assert.Null(gift.Card.PurchaserCustomerId);Assert.Equal(100,gift.Card.RemainingAmount);Assert.Single(await db.Set<GiftCardTransaction>().ToArrayAsync());
        Assert.Empty((await privacy.ExportAsync(s.Customer,default)).PurchasedCards);
    }
    [Fact]
    public async Task GiftPurchase_RequiresCustomerOwnershipAndCodeIsNotPublic()
    {
        await using var db=Memory();var s=await Seed(db);var service=Service(db,s.Customer);
        var r=(await service.CreateAsync(s.Tenant,"owner","purchase",new(10,"NGN","Gift",true),default)).Value!;
        Assert.Equal("forbidden",(await service.CheckoutAsync(Token(r),null,"checkout",default)).Error.Code);
        Assert.Equal("not_found",(await service.RevealAsync("other",Guid.NewGuid(),default)).Error.Code);
        var view=(await service.ResolveAsync(Token(r),default)).Value!;
        Assert.True(view.RequiresCustomerLogin);Assert.DoesNotContain("TenantId",System.Text.Json.JsonSerializer.Serialize(view));
    }

    [Fact]
    public async Task Refund_UnusedGiftVoidsWithLedgerEntry_AndSpentValueCannotBeRefunded()
    {
        await using var db=Memory();var s=await Seed(db);var gift=await Issue(db,s.Tenant,s.Customer);var service=Service(db,s.Customer);
        Assert.True((await service.RefundAsync(s.Tenant,"owner",gift.Request.Id,"refund",default)).IsSuccess);
        Assert.True((await service.RefundAsync(s.Tenant,"owner",gift.Request.Id,"refund",default)).IsSuccess);
        Assert.Equal(GiftCardState.Voided,gift.Card.State);Assert.Equal(0,gift.Card.RemainingAmount);
        Assert.Equal(0,await db.Set<GiftCardTransaction>().Where(x=>x.GiftCardId==gift.Card.Id).SumAsync(x=>x.Delta));
        Assert.Equal(2,await db.Set<GiftCardTransaction>().CountAsync());
        var next=await Issue(db,s.Tenant,s.Customer);var r=(await service.CreateAsync(s.Tenant,"owner","charge",new(10,"NGN","Charge",AllowsGiftCard:true),default)).Value!;
        Assert.True((await service.RedeemAsync(Token(r),"redeem",next.Code,default)).IsSuccess);
        Assert.Equal("commerce.conflict",(await service.RefundAsync(s.Tenant,"owner",next.Request.Id,"spent",default)).Error.Code);
        Assert.Equal(90,next.Card.RemainingAmount);
    }
    [Fact]
    public async Task Expiry_BlocksNewPaymentButDoesNotExpireGiftValue()
    {
        await using var db=Memory();var s=await Seed(db);var gift=await Issue(db,s.Tenant,s.Customer);var service=Service(db,s.Customer);
        gift.Card.IssuedAt=Now.AddYears(-5);
        var request=(await service.CreateAsync(s.Tenant,"owner","expired",new(10,"NGN","Charge",AllowsGiftCard:true),default)).Value!;
        (await db.Set<CommercePaymentRequest>().SingleAsync(x=>x.Id==request.Id)).ExpiresAt=Now.AddDays(-1);await db.SaveChangesAsync();
        Assert.Equal("Expired",(await service.ResolveAsync(Token(request),default)).Value!.State);
        Assert.Equal("commerce.conflict",(await service.CheckoutAsync(Token(request),null,"checkout",default)).Error.Code);
        Assert.Equal("commerce.conflict",(await service.RedeemAsync(Token(request),"redeem",gift.Code,default)).Error.Code);
        Assert.Equal(GiftCardState.Active,gift.Card.State);Assert.Equal(100,gift.Card.RemainingAmount);
    }
    [Fact]
    public async Task PrivacyDuringCheckout_DoesNotPreventConfirmedFinancialIssuance()
    {
        await using var db=Memory();var s=await Seed(db);var service=Service(db,s.Customer);
        var r=(await service.CreateAsync(s.Tenant,"owner","gift",new(100,"NGN","Gift",true),default)).Value!;
        await service.CheckoutAsync(Token(r),"customer","checkout",default);
        await new CommercePrivacyService(db).StageErasureAsync(s.Customer,default);await db.SaveChangesAsync();
        var intent=await db.PaymentIntents.SingleAsync();var completion=new CommercePaymentCompletion(db,new Clock());
        Assert.True((await completion.CompleteAsync(intent,new("paid",intent.ProviderReference,true,false,false,null,100,"NGN"),default)).IsSuccess);
        var card=await db.Set<BusinessGiftCard>().SingleAsync();Assert.Null(card.PurchaserCustomerId);Assert.Equal(100,card.RemainingAmount);
    }
    [Fact]
    public async Task OperationalReport_SeparatesRefundedValueFromRedemption_AndNeverReturnsCodes()
    {
        await using var db=Memory();var s=await Seed(db);var gift=await Issue(db,s.Tenant,s.Customer);
        await Service(db,s.Customer).RefundAsync(s.Tenant,"owner",gift.Request.Id,"refund",default);
        var report=await new GiftCardReportQuery(db,new Access(s.Customer)).GetAsync(s.Tenant,"accountant",Now.AddDays(-1),Now.AddDays(1),default);
        var row=Assert.Single(report.Value!);Assert.Equal(0,row.OutstandingValue);Assert.Equal(0,row.RedeemedValue);Assert.Equal(100,row.IssuedValue);
        Assert.DoesNotContain(gift.Code,System.Text.Json.JsonSerializer.Serialize(row));
        Assert.Equal("forbidden",(await new GiftCardReportQuery(db,new Access(s.Customer)).GetAsync(s.Tenant,"unknown",Now.AddDays(-1),Now,default)).Error.Code);
    }
}
