namespace Adeni.Infrastructure.Tests.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Common;
using Adeni.Application.Finance;
using Adeni.Application.Retention;
using Adeni.Application.Tenancy;
using Adeni.Domain.Auditing;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Retention;
using Adeni.Domain.Retention.Events;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Sprint 37.2-C: provider-billed memberships — activate/renew on confirmed payments, grace, cancel close-out.</summary>
public sealed class MembershipBillingCoordinatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private const string Sub = "auth0|cust-1";

    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; } = ".";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class Guard(bool memberships = true, bool recurring = true) : IRetentionEntitlementGuard
    {
        public Task<bool> IsLoyaltyBasicEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Result> EnsureLoyaltyBasicEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<bool> IsMembershipsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(memberships);

        public Task<Result> EnsureMembershipsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(memberships ? Result.Success() : Result.Failure(RetentionErrorCodes.MembershipsNotEntitledError()));

        public Task<bool> IsRecurringBillingEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(memberships && recurring);

        public Task<Result> EnsureRecurringBillingEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(memberships && recurring
                ? Result.Success()
                : Result.Failure(RetentionErrorCodes.RecurringBillingNotEntitledError()));

        public Task<bool> IsPackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Result> EnsurePackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<bool> IsLoyaltyTiersEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Result> EnsureLoyaltyTiersEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<bool> IsReferralsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Result> EnsureReferralsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<bool> IsCrossLocationRetentionEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Result> EnsureCrossLocationRetentionEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<bool> IsAdvancedRetentionAnalyticsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Result> EnsureAdvancedRetentionAnalyticsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }

    private sealed class FakeProvider(string name = "Stub") : IPaymentProvider
    {
        public string ProviderName { get; } = name;

        public Task<Result<ProviderInitializeResult>> InitializeAsync(ProviderInitializeRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<ProviderPaymentStatus>> GetProviderStatusAsync(string providerReference, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<PaymentWebhookPayload>> ParseWebhookAsync(string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<ProviderRefundResult>> RefundAsync(string providerReference, decimal? amount, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeOrchestrator : IPaymentOrchestrator
    {
        public Dictionary<string, PaymentIntentResponse> ByKey { get; } = [];

        public Dictionary<Guid, PaymentIntentResponse> ById { get; } = [];

        public int InitializeCalls { get; private set; }

        public Task<Result<PaymentIntentResponse>> InitializeCustomerBookingPaymentAsync(
            string customerAuth0Sub,
            InitializePaymentRequest request,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default) =>
            InitializeAsync(request, idempotencyKey, cancellationToken);

        public Task<Result<PaymentIntentResponse>> InitializeAsync(
            InitializePaymentRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        {
            InitializeCalls++;
            var key = idempotencyKey ?? Guid.NewGuid().ToString();
            if (!ByKey.TryGetValue(key, out var intent))
            {
                var id = Guid.NewGuid();
                intent = new PaymentIntentResponse(
                    id, request.TenantId, null, "link", request.Amount ?? 0m, 0m, request.Currency, "pending",
                    $"https://stub.local/{id:N}", $"ref_{id:N}");
                ByKey[key] = intent;
                ById[id] = intent;
            }

            return Task.FromResult(Result.Success(intent));
        }

        public Task<Result<PaymentIntentResponse>> GetAsync(Guid paymentIntentId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ById.TryGetValue(paymentIntentId, out var intent)
                ? Result.Success(intent)
                : Result.Failure<PaymentIntentResponse>(new Error("payment.not_found", "missing")));

        public void SetStatus(Guid id, string status) => ById[id] = ById[id] with { Status = status };

        public Task<Result<PaymentIntentResponse>> CreatePaymentLinkAsync(CreatePaymentLinkRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<PagedResult<PaymentLedgerEntry>>> ListLedgerAsync(Guid tenantId, int page = 1, int pageSize = 50, Guid? locationId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<PaymentWebhookResult>> ProcessWebhookAsync(string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<PaymentIntentResponse>> ConfirmStubCheckoutAsync(string providerReference, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<PaymentIntentResponse>> InitiateRefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<PaymentReceiptResponse>> GetReceiptAsync(Guid paymentIntentId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTenantCustomers : ITenantCustomerService
    {
        public Task<Result<TenantCustomerUpsertResult>> EnsureFromBookingAsync(Guid tenantId, Guid platformCustomerId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("seeded roster row expected");

        public Task<Result<TenantCustomerUpsertResult>> UpsertFromWalkInAsync(Guid tenantId, string displayName, string phone, string? email, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<TenantCustomerDetailResponse>> GetByIdAsync(Guid tenantId, Guid tenantCustomerId, Guid? locationId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<TenantCustomerListResponse>> ListForTenantAsync(
            Guid tenantId, string? search, int page, int pageSize, string? discoverFilter = null, string? sortBy = null,
            ListSortDirection? sortDirection = null, Guid? locationId = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingAudit : IAuditLogWriter
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class Harness
    {
        public AdeniDbContext Db { get; } = new(
            new DbContextOptionsBuilder<AdeniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new TenantContext());

        public DomainEventCollector Events { get; } = new();
        public MutableClock Clock { get; } = new();
        public FakeOrchestrator Payments { get; } = new();
        public CapturingAudit Audit { get; } = new();
        public Guid Tenant { get; } = Guid.NewGuid();
        public Guid Customer { get; } = Guid.NewGuid();
        public Guid TenantCustomer { get; } = Guid.NewGuid();
        public Guid ServiceA { get; } = Guid.NewGuid();

        public MembershipBillingCoordinator Coordinator(
            string environment = "Testing", string provider = "Stub", bool memberships = true, bool recurring = true) =>
            new(
                Db, new Guard(memberships, recurring), Payments, new FakeProvider(provider), new FakeTenantCustomers(),
                Events, new Env(environment), Clock, NullLogger<MembershipBillingCoordinator>.Instance, Audit);

        public async Task SeedAsync()
        {
            Db.Add(new Tenant { Id = Tenant, Name = "Salon", CreatedAt = Start });
            Db.Add(new Customer { Id = Customer, Auth0Sub = Sub, Name = "Ada", CreatedAt = Start });
            Db.Add(new TenantCustomer
            {
                Id = TenantCustomer, TenantId = Tenant, CustomerId = Customer, DisplayName = "Ada",
                CreatedAt = Start, UpdatedAt = Start, LastSeenAt = Start,
            });
            Db.Add(new Domain.Booking.ServiceOffering { Id = ServiceA, TenantId = Tenant, Name = "Haircut", PriceAmount = 5000 });
            await Db.SaveChangesAsync();
        }

        public async Task<MembershipPlan> PlanAsync(
            MembershipRolloverPolicy rollover = MembershipRolloverPolicy.None, int graceDays = 7, int quantity = 2)
        {
            var plan = new MembershipPlan
            {
                Id = Guid.NewGuid(), TenantId = Tenant, Name = "Fade Club", Price = 25000m, Currency = "NGN",
                BillingInterval = MembershipBillingInterval.Monthly, RolloverPolicy = rollover,
                CancellationPolicy = MembershipCancellationPolicy.EndOfCycle, AllowFreeze = true,
                PastDueGraceDays = graceDays, IsActive = true, CreatedAt = Start, UpdatedAt = Start,
            };
            plan.Benefits.Add(new MembershipPlanBenefit
            {
                Id = Guid.NewGuid(), TenantId = Tenant, MembershipPlanId = plan.Id, ServiceOfferingId = ServiceA,
                ServiceName = "Haircut", QuantityPerCycle = quantity, SortOrder = 0,
            });
            Db.Add(plan);
            await Db.SaveChangesAsync();
            return plan;
        }

        public async Task<MembershipSubscribeResponse> SubscribeAsync(Guid planId, string key = "k1")
        {
            var result = await Coordinator().StartOnlineSubscriptionAsync(Tenant, Sub, planId, key);
            Assert.True(result.IsSuccess, result.Error?.Code);
            return result.Value!;
        }

        public async Task<MembershipSubscribeResponse> SubscribeAndPayAsync(Guid planId)
        {
            var sub = await SubscribeAsync(planId);
            Assert.True(await Coordinator().ApplyPaymentCompletedAsync(Tenant, sub.PaymentIntentId, 25000m, "NGN", "ref"));
            return sub;
        }

        public Task<CustomerMembership> LoadAsync(Guid id) =>
            Db.Set<CustomerMembership>().Include(x => x.Benefits).Include(x => x.Balances).AsNoTracking()
                .SingleAsync(x => x.Id == id);
    }

    private static async Task<Harness> NewAsync()
    {
        var h = new Harness();
        await h.SeedAsync();
        return h;
    }

    [Fact]
    public async Task Subscribe_CreatesPendingMembershipAndOneIntent_WithoutGrantingCredits()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();

        var sub = await h.SubscribeAsync(plan.Id);

        Assert.Equal("pending", sub.Status);
        Assert.False(string.IsNullOrEmpty(sub.CheckoutUrl));
        var membership = await h.LoadAsync(sub.CustomerMembershipId);
        Assert.Equal(CustomerMembershipStatus.Pending, membership.Status);
        Assert.Equal(MembershipEnrolChannel.Online, membership.EnrolChannel);
        Assert.Empty(membership.Balances);
        Assert.Single(await h.Db.Set<MembershipPayment>().ToListAsync());
    }

    [Fact]
    public async Task Subscribe_SameKey_ReplaysSameIntent()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();

        var first = await h.SubscribeAsync(plan.Id, "dup");
        var second = await h.SubscribeAsync(plan.Id, "dup");

        Assert.Equal(first.CustomerMembershipId, second.CustomerMembershipId);
        Assert.Equal(first.PaymentIntentId, second.PaymentIntentId);
        Assert.Equal(1, await h.Db.Set<CustomerMembership>().CountAsync());
        Assert.Equal(1, await h.Db.Set<MembershipPayment>().CountAsync());
    }

    [Fact]
    public async Task Subscribe_NewKeyAfterAbandonedCheckout_ResumesPendingMembership()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();

        var first = await h.SubscribeAsync(plan.Id, "one");
        var second = await h.SubscribeAsync(plan.Id, "two");

        Assert.Equal(first.CustomerMembershipId, second.CustomerMembershipId);
        Assert.Equal(first.PaymentIntentId, second.PaymentIntentId);
        Assert.Equal(1, await h.Db.Set<CustomerMembership>().CountAsync());
    }

    [Fact]
    public async Task Subscribe_WhenAlreadyActive_Fails()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        await h.SubscribeAndPayAsync(plan.Id);

        var again = await h.Coordinator().StartOnlineSubscriptionAsync(h.Tenant, Sub, plan.Id, "other");

        Assert.True(again.IsFailure);
        Assert.Equal(RetentionErrorCodes.MembershipAlreadyActive, again.Error.Code);
    }

    [Fact]
    public async Task Subscribe_WithoutKey_Fails()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();

        var result = await h.Coordinator().StartOnlineSubscriptionAsync(h.Tenant, Sub, plan.Id, null);

        Assert.Equal(RetentionErrorCodes.IdempotencyKeyRequired, result.Error.Code);
    }

    [Theory]
    [InlineData("Production", "Stub")]
    [InlineData("Staging", "Paystack")]
    [InlineData("Testing", "Paystack")]
    public async Task Subscribe_OutsideDevStub_ReturnsUnavailable(string environment, string provider)
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();

        var result = await h.Coordinator(environment, provider).StartOnlineSubscriptionAsync(h.Tenant, Sub, plan.Id, "k");

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.OnlineSubscriptionUnavailable, result.Error.Code);
        Assert.Empty(await h.Db.Set<CustomerMembership>().ToListAsync());
    }

    [Fact]
    public async Task Subscribe_WithoutRecurringEntitlement_ReturnsEntitlementError()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();

        var result = await h.Coordinator(recurring: false).StartOnlineSubscriptionAsync(h.Tenant, Sub, plan.Id, "k");

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.RecurringBillingNotEntitled, result.Error.Code);
    }

    [Fact]
    public async Task PaymentCompleted_ActivatesAndGrantsCycleOneOnce_EvenWhenDeliveredTwice()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var sub = await h.SubscribeAsync(plan.Id);
        var coordinator = h.Coordinator();

        Assert.True(await coordinator.ApplyPaymentCompletedAsync(h.Tenant, sub.PaymentIntentId, 25000m, "NGN", "ref-1"));
        Assert.True(await coordinator.ApplyPaymentCompletedAsync(h.Tenant, sub.PaymentIntentId, 25000m, "NGN", "ref-1"));

        var membership = await h.LoadAsync(sub.CustomerMembershipId);
        Assert.Equal(CustomerMembershipStatus.Active, membership.Status);
        Assert.Equal(1, membership.CurrentCycleNumber);
        Assert.Equal(Start.AddMonths(1), membership.CurrentCycleEnd);
        Assert.Single(membership.Balances);
        Assert.Equal(2, membership.Balances.Single().Granted);
        Assert.StartsWith("stub_", membership.ProviderMandateRef);
        Assert.Equal(1, await h.Db.Set<BenefitTransaction>().CountAsync());
        var payment = await h.Db.Set<MembershipPayment>().SingleAsync();
        Assert.Equal(MembershipPaymentStatus.Succeeded, payment.Status);
        Assert.Single(h.Events.TakeAll().OfType<CustomerMembershipActivated>());
        Assert.Contains(h.Audit.Entries, e => e.Action == AuditActions.RetentionMembershipActivated);
    }

    [Fact]
    public async Task PaymentCompleted_AmountMismatch_DoesNotActivate()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var sub = await h.SubscribeAsync(plan.Id);

        await h.Coordinator().ApplyPaymentCompletedAsync(h.Tenant, sub.PaymentIntentId, 1m, "NGN", "ref");

        var membership = await h.LoadAsync(sub.CustomerMembershipId);
        Assert.Equal(CustomerMembershipStatus.Pending, membership.Status);
        Assert.Empty(membership.Balances);
        var payment = await h.Db.Set<MembershipPayment>().SingleAsync();
        Assert.Equal(MembershipPaymentStatus.Unapplied, payment.Status);
        Assert.Equal("amount_mismatch", payment.FailureReason);
    }

    [Fact]
    public async Task PaymentCompleted_ForUnknownIntent_IsIgnored()
    {
        var h = await NewAsync();

        var handled = await h.Coordinator().ApplyPaymentCompletedAsync(h.Tenant, Guid.NewGuid(), 10m, "NGN", "x");

        Assert.False(handled);
    }

    [Fact]
    public async Task PaymentCompleted_OtherTenant_IsIgnored()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var sub = await h.SubscribeAsync(plan.Id);

        var handled = await h.Coordinator().ApplyPaymentCompletedAsync(Guid.NewGuid(), sub.PaymentIntentId, 25000m, "NGN", "x");

        Assert.False(handled);
        Assert.Equal(CustomerMembershipStatus.Pending, (await h.LoadAsync(sub.CustomerMembershipId)).Status);
    }

    [Fact]
    public async Task PaymentCompleted_AfterMembershipCancelledWhilePending_IsFlaggedNotActivated()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var sub = await h.SubscribeAsync(plan.Id);
        var pending = await h.Db.Set<CustomerMembership>().SingleAsync();
        pending.Status = CustomerMembershipStatus.Cancelled;
        await h.Db.SaveChangesAsync();

        await h.Coordinator().ApplyPaymentCompletedAsync(h.Tenant, sub.PaymentIntentId, 25000m, "NGN", "ref");

        var payment = await h.Db.Set<MembershipPayment>().SingleAsync();
        Assert.Equal(MembershipPaymentStatus.Unapplied, payment.Status);
        Assert.Empty(await h.Db.Set<BenefitBalance>().ToListAsync());
    }

    [Fact]
    public async Task Sweep_OnlineMembershipAtCycleEnd_StartsRenewalChargeAndMarksPastDue()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var sub = await h.SubscribeAndPayAsync(plan.Id);
        h.Clock.Now = Start.AddMonths(1).AddMinutes(1);

        var run = await h.Coordinator().ProcessDueAsync(50);

        Assert.Equal(1, run.RenewalsStarted);
        Assert.Equal(1, run.PastDue);
        var membership = await h.LoadAsync(sub.CustomerMembershipId);
        Assert.Equal(CustomerMembershipStatus.PastDue, membership.Status);
        Assert.NotNull(membership.PastDueSince);
        var renewal = await h.Db.Set<MembershipPayment>().SingleAsync(x => x.Kind == MembershipPaymentKind.Renewal);
        Assert.Equal(2, renewal.CycleNumber);
        Assert.Equal(MembershipPaymentStatus.Pending, renewal.Status);

        // Re-running does not create a second charge for the same cycle.
        var again = await h.Coordinator().ProcessDueAsync(50);
        Assert.Equal(0, again.RenewalsStarted);
        Assert.Equal(1, await h.Db.Set<MembershipPayment>().CountAsync(x => x.Kind == MembershipPaymentKind.Renewal));
    }

    [Fact]
    public async Task RenewalPayment_AdvancesCycleAndGrantsNewCredits_ForfeitingUnusedWithoutRollover()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var sub = await h.SubscribeAndPayAsync(plan.Id);
        h.Clock.Now = Start.AddMonths(1).AddMinutes(1);
        await h.Coordinator().ProcessDueAsync(50);
        var renewal = await h.Db.Set<MembershipPayment>().SingleAsync(x => x.Kind == MembershipPaymentKind.Renewal);
        h.Events.TakeAll();

        Assert.True(await h.Coordinator().ApplyPaymentCompletedAsync(h.Tenant, renewal.PaymentIntentId, 25000m, "NGN", "ref-2"));
        Assert.True(await h.Coordinator().ApplyPaymentCompletedAsync(h.Tenant, renewal.PaymentIntentId, 25000m, "NGN", "ref-2"));

        var membership = await h.LoadAsync(sub.CustomerMembershipId);
        Assert.Equal(CustomerMembershipStatus.Active, membership.Status);
        Assert.Null(membership.PastDueSince);
        Assert.Equal(2, membership.CurrentCycleNumber);
        Assert.Equal(2, membership.Balances.Count);
        var cycleOne = membership.Balances.Single(x => x.CycleNumber == 1);
        var cycleTwo = membership.Balances.Single(x => x.CycleNumber == 2);
        Assert.Equal(0, cycleOne.Remaining);
        Assert.Equal(2, cycleOne.Forfeited);
        Assert.Equal(2, cycleTwo.Granted);
        Assert.Equal(2, cycleTwo.Remaining);
        Assert.Single(h.Events.TakeAll().OfType<CustomerMembershipRenewed>());
    }

    [Fact]
    public async Task RenewalPayment_WithRollover_CarriesUnusedCreditsOneCycleOnly()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync(MembershipRolloverPolicy.CarryOverOneCycle);
        var sub = await h.SubscribeAndPayAsync(plan.Id);

        async Task RenewAsync(DateTimeOffset at)
        {
            h.Clock.Now = at;
            await h.Coordinator().ProcessDueAsync(50);
            var open = await h.Db.Set<MembershipPayment>()
                .Where(x => x.Kind == MembershipPaymentKind.Renewal && x.Status == MembershipPaymentStatus.Pending)
                .SingleAsync();
            await h.Coordinator().ApplyPaymentCompletedAsync(h.Tenant, open.PaymentIntentId, 25000m, "NGN", "r");
        }

        await RenewAsync(Start.AddMonths(1).AddMinutes(1));
        var afterFirst = await h.LoadAsync(sub.CustomerMembershipId);
        var cycleTwo = afterFirst.Balances.Single(x => x.CycleNumber == 2);
        Assert.Equal(4, cycleTwo.Granted); // 2 new + 2 carried
        Assert.Equal(2, cycleTwo.CarriedIn);

        await RenewAsync(afterFirst.CurrentCycleEnd!.Value.AddMinutes(1));
        var afterSecond = await h.LoadAsync(sub.CustomerMembershipId);
        var cycleThree = afterSecond.Balances.Single(x => x.CycleNumber == 3);
        // Cycle 2 used nothing: only its own 2 credits may carry; the 2 carried-in expire.
        Assert.Equal(4, cycleThree.Granted);
        Assert.Equal(2, cycleThree.CarriedIn);
    }

    [Fact]
    public async Task PaymentFailed_OnRenewal_MarksPastDueAndRaisesEventOnce()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var sub = await h.SubscribeAndPayAsync(plan.Id);
        h.Clock.Now = Start.AddMonths(1).AddMinutes(1);
        await h.Coordinator().ProcessDueAsync(50);
        var renewal = await h.Db.Set<MembershipPayment>().SingleAsync(x => x.Kind == MembershipPaymentKind.Renewal);
        h.Events.TakeAll();

        Assert.True(await h.Coordinator().ApplyPaymentFailedAsync(h.Tenant, renewal.PaymentIntentId, "declined"));
        Assert.True(await h.Coordinator().ApplyPaymentFailedAsync(h.Tenant, renewal.PaymentIntentId, "declined"));

        var membership = await h.LoadAsync(sub.CustomerMembershipId);
        Assert.Equal(CustomerMembershipStatus.PastDue, membership.Status);
        Assert.Equal(1, membership.FailedPaymentCount);
        Assert.Single(h.Events.TakeAll().OfType<CustomerMembershipPaymentFailed>());
        Assert.Equal(MembershipPaymentStatus.Failed, (await h.Db.Set<MembershipPayment>().SingleAsync(x => x.Id == renewal.Id)).Status);
    }

    [Fact]
    public async Task Sweep_PastDueBeyondGrace_ExpiresAndForfeitsCredits()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync(graceDays: 3);
        var sub = await h.SubscribeAndPayAsync(plan.Id);
        h.Clock.Now = Start.AddMonths(1).AddMinutes(1);
        await h.Coordinator().ProcessDueAsync(50);
        h.Events.TakeAll();

        h.Clock.Now = Start.AddMonths(1).AddDays(3).AddMinutes(1);
        var run = await h.Coordinator().ProcessDueAsync(50);

        Assert.Equal(1, run.Expired);
        var membership = await h.LoadAsync(sub.CustomerMembershipId);
        Assert.Equal(CustomerMembershipStatus.Expired, membership.Status);
        Assert.All(membership.Balances, b => Assert.Equal(0, b.Remaining));
        Assert.Single(h.Events.TakeAll().OfType<CustomerMembershipExpired>());
        Assert.Contains(h.Audit.Entries, e => e.Action == AuditActions.RetentionMembershipExpired);
    }

    [Fact]
    public async Task Sweep_CancelPendingAtCycleEnd_ClosesMembershipWithoutAnotherCharge()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var sub = await h.SubscribeAndPayAsync(plan.Id);
        var tracked = await h.Db.Set<CustomerMembership>().SingleAsync();
        Assert.True(tracked.RequestCancel("customer", null, Start.AddDays(2)).IsSuccess);
        await h.Db.SaveChangesAsync();
        Assert.Equal(CustomerMembershipStatus.CancelPending, tracked.Status);

        h.Clock.Now = Start.AddMonths(1).AddMinutes(1);
        var run = await h.Coordinator().ProcessDueAsync(50);

        Assert.Equal(1, run.CancellationsCompleted);
        Assert.Equal(0, run.RenewalsStarted);
        var membership = await h.LoadAsync(sub.CustomerMembershipId);
        Assert.Equal(CustomerMembershipStatus.Cancelled, membership.Status);
        Assert.Equal(1, await h.Db.Set<MembershipPayment>().CountAsync());
    }

    [Fact]
    public async Task Sweep_NoRecurringEntitlement_DoesNotStartRenewalCharge()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        await h.SubscribeAndPayAsync(plan.Id);
        h.Clock.Now = Start.AddMonths(1).AddMinutes(1);

        var run = await h.Coordinator(recurring: false).ProcessDueAsync(50);

        Assert.Equal(0, run.RenewalsStarted);
        Assert.Equal(1, run.PastDue);
        Assert.Equal(1, await h.Db.Set<MembershipPayment>().CountAsync());
    }

    [Fact]
    public async Task Reconcile_AppliesCompletedIntentWhoseEventWasMissed()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var sub = await h.SubscribeAsync(plan.Id);
        h.Payments.SetStatus(sub.PaymentIntentId, "completed");
        h.Clock.Now = Start.AddMinutes(10);

        var applied = await h.Coordinator().ReconcilePendingAsync(50);

        Assert.Equal(1, applied);
        Assert.Equal(CustomerMembershipStatus.Active, (await h.LoadAsync(sub.CustomerMembershipId)).Status);
    }

    [Fact]
    public async Task Reconcile_RecordsFailedIntent()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var sub = await h.SubscribeAsync(plan.Id);
        h.Payments.SetStatus(sub.PaymentIntentId, "failed");
        h.Clock.Now = Start.AddMinutes(10);

        await h.Coordinator().ReconcilePendingAsync(50);

        Assert.Equal(MembershipPaymentStatus.Failed, (await h.Db.Set<MembershipPayment>().SingleAsync()).Status);
        Assert.Equal(CustomerMembershipStatus.Pending, (await h.LoadAsync(sub.CustomerMembershipId)).Status);
    }
}
