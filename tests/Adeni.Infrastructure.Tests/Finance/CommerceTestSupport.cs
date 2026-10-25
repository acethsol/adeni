namespace Adeni.Infrastructure.Tests.Finance;

using Adeni.Application.Finance;
using Adeni.Domain.Common;
using Adeni.Domain.Finance;
using Adeni.Domain.Identity;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Finance;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

internal static class CommerceTestSupport
{
    internal static readonly DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    internal sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    internal sealed class Host(string name = "Testing") : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "/tmp";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    internal sealed class Access(Guid customer) : IFinanceCommerceAccessQuery
    {
        public Task<bool> CanManageAsync(Guid t, string actor, CancellationToken ct) => Task.FromResult(actor is "owner" or "manager");
        public Task<bool> CanReadAsync(Guid t, string actor, CancellationToken ct) => Task.FromResult(actor is "owner" or "manager" or "accountant");
        public Task<Guid?> CustomerIdAsync(string actor, CancellationToken ct) => Task.FromResult<Guid?>(actor == "customer" ? customer : null);
    }
    internal sealed class Lifecycle : ICommercePaymentLifecycle
    {
        public Task<Result<ProviderRefundResult>> RefundCommerceAsync(Guid id, CancellationToken ct) => Task.FromResult(Result.Success(new ProviderRefundResult("stub", 100m, "processed")));
        public Task<Result<CommerceCheckoutView>> InitializeCommerceAsync(Guid id, CancellationToken ct) => Task.FromResult(Result.Success(new CommerceCheckoutView("PaymentPending", "/checkout/stub")));
    }
    internal static AdeniDbContext Memory() => new(new DbContextOptionsBuilder<AdeniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new TenantContext());
    internal static CommerceService Service(AdeniDbContext db, Guid customer, string env = "Testing") => new(db, new Access(customer), new(Options.Create(new CommerceOptions()), new Host(env)), new Lifecycle(), new StubPaymentProvider(db, Options.Create(new FinanceOptions())), new Host(env), new Clock());
    internal static async Task<(Guid Tenant, Guid Customer)> Seed(AdeniDbContext db)
    {
        var tenant = Guid.NewGuid(); var customer = Guid.NewGuid();
        db.Add(new Tenant { Id = tenant, Name = "Business gift test", CreatedAt = Now });
        db.Add(new Customer { Id = customer, Name = "Purchaser", Auth0Sub = $"customer-{customer:N}", CreatedAt = Now });
        await db.SaveChangesAsync(); return (tenant, customer);
    }
    internal static string Token(CommerceRequestView r) => r.ShareUrl.Split("#t=")[1];
    internal static async Task<(CommerceRequestView Request, BusinessGiftCard Card, string Code)> Issue(AdeniDbContext db, Guid tenant, Guid customer, decimal amount = 100m)
    {
        var service = Service(db, customer);
        var created = await service.CreateAsync(tenant, "owner", Guid.NewGuid().ToString(), new(amount, "NGN", "Gift purchase", true), default);
        Assert.True(created.IsSuccess, created.Error?.Code); var r = created.Value!;
        Assert.True((await service.CheckoutAsync(Token(r), "customer", "checkout", default)).IsSuccess);
        var attempt = await db.Set<CommercePaymentAttempt>().SingleAsync(x => x.RequestId == r.Id);
        var intent = await db.PaymentIntents.SingleAsync(x => x.Id == attempt.PaymentIntentId);
        var result = await new CommercePaymentCompletion(db, new Clock()).CompleteAsync(intent, new("stub.paid", intent.ProviderReference, true, false, false, null, amount, "NGN"), default);
        Assert.True(result.IsSuccess, result.Error?.Code);
        var card = await db.Set<BusinessGiftCard>().SingleAsync(x => x.PurchaseRequestId == r.Id);
        return (r, card, new CommerceCredentials(Options.Create(new CommerceOptions()), new Host()).GiftCode(card));
    }
}
