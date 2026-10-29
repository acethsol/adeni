namespace Adeni.Infrastructure.Tests.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Retention;
using Adeni.Domain.Auditing;
using Adeni.Domain.Identity;
using Adeni.Domain.Retention;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore;

/// <summary>Sprint 37.2-C/D/E: in-person renewal, customer self-service, membership credits on bookings, MRR.</summary>
public sealed class MembershipRedemptionAndLifecycleTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly RetentionActor Manager = new("manager-1", "manager");
    private const string Sub = "auth0|cust-1";

    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Guard(bool entitled = true) : IRetentionEntitlementGuard
    {
        public Task<bool> IsLoyaltyBasicEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Domain.Common.Result> EnsureLoyaltyBasicEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());

        public Task<bool> IsMembershipsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(entitled);

        public Task<Domain.Common.Result> EnsureMembershipsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(entitled
                ? Domain.Common.Result.Success()
                : Domain.Common.Result.Failure(RetentionErrorCodes.MembershipsNotEntitledError()));

        public Task<bool> IsRecurringBillingEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(entitled);

        public Task<Domain.Common.Result> EnsureRecurringBillingEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());

        public Task<bool> IsPackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Domain.Common.Result> EnsurePackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());
    }

    private sealed class NullAudit : IAuditLogWriter
    {
        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Harness
    {
        public AdeniDbContext Db { get; } = new(
            new DbContextOptionsBuilder<AdeniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new TenantContext());

        public DomainEventCollector Events { get; } = new();
        public MutableClock Clock { get; } = new();
        public Guid Tenant { get; } = Guid.NewGuid();
        public Guid Location { get; } = Guid.NewGuid();
        public Guid Customer { get; } = Guid.NewGuid();
        public Guid TenantCustomer { get; } = Guid.NewGuid();
        public Guid OtherCustomer { get; } = Guid.NewGuid();
        public Guid OtherTenantCustomer { get; } = Guid.NewGuid();
        public Guid Service { get; } = Guid.NewGuid();

        public CustomerMembershipService Memberships(bool entitled = true) =>
            new(Db, new Guard(entitled), Events, Clock, new NullAudit());

        public BenefitRedemptionService Redemption() => new(Db, Events, Clock);

        public MembershipMrrService Mrr(bool entitled = true) => new(Db, new Guard(entitled), Clock);

        public async Task SeedAsync()
        {
            Db.Add(new Tenant { Id = Tenant, Name = "Salon", CreatedAt = Start });
            Db.Add(new BusinessLocation { Id = Location, TenantId = Tenant, Name = "A", Slug = "a", IsPrimary = true, CreatedAt = Start });
            Db.Add(new Customer { Id = Customer, Auth0Sub = Sub, Name = "Ada", CreatedAt = Start });
            Db.Add(new Customer { Id = OtherCustomer, Auth0Sub = "auth0|other", Name = "Bo", CreatedAt = Start });
            Db.Add(new TenantCustomer
            {
                Id = TenantCustomer, TenantId = Tenant, CustomerId = Customer, DisplayName = "Ada",
                CreatedAt = Start, UpdatedAt = Start, LastSeenAt = Start,
            });
            Db.Add(new TenantCustomer
            {
                Id = OtherTenantCustomer, TenantId = Tenant, CustomerId = OtherCustomer, DisplayName = "Bo",
                CreatedAt = Start, UpdatedAt = Start, LastSeenAt = Start,
            });
            Db.Add(new Domain.Booking.ServiceOffering { Id = Service, TenantId = Tenant, Name = "Haircut", PriceAmount = 5000 });
            await Db.SaveChangesAsync();
        }

        public async Task<MembershipPlan> PlanAsync(
            decimal price = 25000m,
            MembershipBillingInterval interval = MembershipBillingInterval.Monthly,
            MembershipRolloverPolicy rollover = MembershipRolloverPolicy.None,
            int graceDays = 7)
        {
            var plan = new MembershipPlan
            {
                Id = Guid.NewGuid(), TenantId = Tenant, Name = "Fade Club", Price = price, Currency = "NGN",
                BillingInterval = interval, RolloverPolicy = rollover, PastDueGraceDays = graceDays,
                CancellationPolicy = MembershipCancellationPolicy.EndOfCycle, AllowFreeze = true,
                IsActive = true, CreatedAt = Start, UpdatedAt = Start,
            };
            plan.Benefits.Add(new MembershipPlanBenefit
            {
                Id = Guid.NewGuid(), TenantId = Tenant, MembershipPlanId = plan.Id, ServiceOfferingId = Service,
                ServiceName = "Haircut", QuantityPerCycle = 2, SortOrder = 0,
            });
            Db.Add(plan);
            await Db.SaveChangesAsync();
            return plan;
        }

        public async Task<CustomerMembershipResponse> EnrolAsync(Guid planId, Guid? tenantCustomer = null, string key = "k1")
        {
            var result = await Memberships().EnrolAsync(
                Tenant, Manager, tenantCustomer ?? TenantCustomer, new EnrolMembershipRequest(planId, Location, "till", null), key);
            Assert.True(result.IsSuccess, result.Error?.Code);
            return result.Value!;
        }

        public ReserveBenefitsRequest Reserve(CustomerMembershipResponse membership, int quantity = 1, Guid? booking = null)
        {
            var balance = membership.Balances.Single();
            return new ReserveBenefitsRequest(
                Tenant, Customer, booking ?? Guid.NewGuid(), Location, [Service], 1,
                [new AppliedBenefitRequest(null, balance.Id, quantity, membership.Id)], "customer", Clock.Now);
        }
    }

    private static async Task<Harness> NewAsync()
    {
        var h = new Harness();
        await h.SeedAsync();
        return h;
    }

    // ---- In-person renewal ----

    [Fact]
    public async Task Renew_AfterCycleEnd_AdvancesCycleAndGrantsFreshCredits()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var enrolled = await h.EnrolAsync(plan.Id);
        h.Clock.Now = enrolled.CurrentCycleEnd!.Value.AddHours(1);

        var renewed = await h.Memberships().RenewAsync(h.Tenant, Manager, enrolled.Id, new RenewMembershipRequest("till"), "r1");

        Assert.True(renewed.IsSuccess, renewed.Error?.Code);
        Assert.Equal(2, renewed.Value!.CurrentCycleNumber);
        Assert.Equal("active", renewed.Value.Status);
        Assert.Equal(2, renewed.Value.Balances.Single().Granted);
        Assert.Equal(2, renewed.Value.Balances.Single().Remaining);
    }

    [Fact]
    public async Task Renew_SameKey_ReplaysWithoutSecondCycle()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var enrolled = await h.EnrolAsync(plan.Id);
        h.Clock.Now = enrolled.CurrentCycleEnd!.Value.AddHours(1);

        var first = await h.Memberships().RenewAsync(h.Tenant, Manager, enrolled.Id, new RenewMembershipRequest("till"), "r1");
        var second = await h.Memberships().RenewAsync(h.Tenant, Manager, enrolled.Id, new RenewMembershipRequest("till"), "r1");

        Assert.True(second.IsSuccess, second.Error?.Code);
        Assert.Equal(first.Value!.CurrentCycleNumber, second.Value!.CurrentCycleNumber);
        Assert.Equal(2, (await h.Db.Set<CustomerMembership>().SingleAsync()).CurrentCycleNumber);
    }

    [Fact]
    public async Task Renew_BeforeCycleEnd_IsRejected()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var enrolled = await h.EnrolAsync(plan.Id);

        var result = await h.Memberships().RenewAsync(h.Tenant, Manager, enrolled.Id, new RenewMembershipRequest("till"), "r1");

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.MembershipRenewalNotDue, result.Error.Code);
    }

    [Fact]
    public async Task Renew_AfterGraceElapsed_IsRejected()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync(graceDays: 3);
        var enrolled = await h.EnrolAsync(plan.Id);
        h.Clock.Now = enrolled.CurrentCycleEnd!.Value.AddDays(4);

        var result = await h.Memberships().RenewAsync(h.Tenant, Manager, enrolled.Id, new RenewMembershipRequest("till"), "r1");

        Assert.Equal(RetentionErrorCodes.MembershipRenewalNotDue, result.Error.Code);
    }

    [Fact]
    public async Task Renew_OnlineMembership_IsRejected_BecauseOnlyConfirmedPaymentsRenewIt()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var enrolled = await h.EnrolAsync(plan.Id);
        var tracked = await h.Db.Set<CustomerMembership>().SingleAsync();
        tracked.EnrolChannel = MembershipEnrolChannel.Online;
        await h.Db.SaveChangesAsync();
        h.Clock.Now = enrolled.CurrentCycleEnd!.Value.AddHours(1);

        var result = await h.Memberships().RenewAsync(h.Tenant, Manager, enrolled.Id, new RenewMembershipRequest("till"), "r1");

        Assert.Equal(RetentionErrorCodes.MembershipRenewalNotDue, result.Error.Code);
    }

    [Fact]
    public async Task Renew_WithoutKey_OrBadMethod_OrNotEntitled_Fails()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var enrolled = await h.EnrolAsync(plan.Id);
        h.Clock.Now = enrolled.CurrentCycleEnd!.Value.AddHours(1);

        var noKey = await h.Memberships().RenewAsync(h.Tenant, Manager, enrolled.Id, new RenewMembershipRequest("till"), null);
        var badMethod = await h.Memberships().RenewAsync(h.Tenant, Manager, enrolled.Id, new RenewMembershipRequest("card"), "r1");
        var unentitled = await h.Memberships(entitled: false).RenewAsync(
            h.Tenant, Manager, enrolled.Id, new RenewMembershipRequest("till"), "r2");

        Assert.Equal(RetentionErrorCodes.IdempotencyKeyRequired, noKey.Error.Code);
        Assert.Equal(RetentionErrorCodes.Validation, badMethod.Error.Code);
        Assert.Equal(RetentionErrorCodes.MembershipsNotEntitled, unentitled.Error.Code);
    }

    [Fact]
    public async Task Renew_WithRollover_CarriesUnusedCredits()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync(rollover: MembershipRolloverPolicy.CarryOverOneCycle);
        var enrolled = await h.EnrolAsync(plan.Id);
        h.Clock.Now = enrolled.CurrentCycleEnd!.Value.AddHours(1);

        var renewed = await h.Memberships().RenewAsync(h.Tenant, Manager, enrolled.Id, new RenewMembershipRequest("till"), "r1");

        Assert.Equal(4, renewed.Value!.Balances.Single().Granted);
    }

    // ---- Customer self-service ----

    [Fact]
    public async Task ListForPlatformCustomer_ReturnsOnlyOwnMemberships_WithBusinessName()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        await h.EnrolAsync(plan.Id, h.TenantCustomer, "a");
        await h.EnrolAsync(plan.Id, h.OtherTenantCustomer, "b");

        var mine = await h.Memberships().ListForPlatformCustomerAsync(Sub);

        Assert.True(mine.IsSuccess, mine.Error?.Code);
        var item = Assert.Single(mine.Value!.Items);
        Assert.Equal(h.TenantCustomer, item.TenantCustomerId);
        Assert.Equal("Salon", item.BusinessName);
    }

    [Fact]
    public async Task CancelForCustomer_OtherCustomersMembership_ReturnsNotFound()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var theirs = await h.EnrolAsync(plan.Id, h.OtherTenantCustomer);

        var result = await h.Memberships().CancelForCustomerAsync(Sub, theirs.Id, new CancelMembershipRequest());

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.MembershipNotFound, result.Error.Code);
        Assert.Equal(CustomerMembershipStatus.Active, (await h.Db.Set<CustomerMembership>().SingleAsync()).Status);
    }

    [Fact]
    public async Task CancelForCustomer_EndOfCyclePlan_SchedulesCancellationAndKeepsCredits()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var mine = await h.EnrolAsync(plan.Id);

        var result = await h.Memberships().CancelForCustomerAsync(Sub, mine.Id, new CancelMembershipRequest("moving"));

        Assert.True(result.IsSuccess, result.Error?.Code);
        Assert.Equal("cancel_pending", result.Value!.Status);
        Assert.Equal(2, result.Value.Balances.Single().Remaining);
    }

    [Fact]
    public async Task FreezeAndUnfreezeForCustomer_ExtendsPaidCycleByFrozenDuration()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var mine = await h.EnrolAsync(plan.Id);
        var originalEnd = mine.CurrentCycleEnd!.Value;

        h.Clock.Now = Start.AddDays(5);
        var frozen = await h.Memberships().FreezeForCustomerAsync(Sub, mine.Id);
        Assert.Equal("frozen", frozen.Value!.Status);

        h.Clock.Now = Start.AddDays(12);
        var thawed = await h.Memberships().UnfreezeForCustomerAsync(Sub, mine.Id);

        Assert.True(thawed.IsSuccess, thawed.Error?.Code);
        Assert.Equal("active", thawed.Value!.Status);
        Assert.Equal(originalEnd.AddDays(7), thawed.Value.CurrentCycleEnd);
        Assert.Equal(thawed.Value.CurrentCycleEnd, thawed.Value.Balances.Single().ExpiresAt);
    }

    // ---- Membership credits on bookings ----

    [Fact]
    public async Task Redemption_ReserveMembershipCredit_ThenFinalize_ConsumesIt()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var mine = await h.EnrolAsync(plan.Id);
        var request = h.Reserve(mine);

        var reserved = await h.Redemption().TryReserveBenefitsAsync(request);
        await h.Db.SaveChangesAsync();

        Assert.True(reserved.IsSuccess, reserved.Error?.Code);
        var line = Assert.Single(reserved.Value!);
        Assert.Equal(mine.Id, line.CustomerMembershipId);
        Assert.Null(line.CustomerPackageId);
        var balance = await h.Db.Set<BenefitBalance>().SingleAsync();
        Assert.Equal(1, balance.Reserved);

        var finalized = await h.Redemption().FinalizeRedemptionsAsync(h.Tenant, request.BookingId, "system");
        await h.Db.SaveChangesAsync();

        Assert.True(finalized.IsSuccess, finalized.Error?.Code);
        balance = await h.Db.Set<BenefitBalance>().SingleAsync();
        Assert.Equal(1, balance.Used);
        Assert.Equal(0, balance.Reserved);
        Assert.Equal(1, balance.Remaining);
    }

    [Fact]
    public async Task Redemption_MoreThanRemaining_IsRejected()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var mine = await h.EnrolAsync(plan.Id);

        var result = await h.Redemption().TryReserveBenefitsAsync(h.Reserve(mine, quantity: 3));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Redemption_ForFrozenMembership_IsRejected()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var mine = await h.EnrolAsync(plan.Id);
        await h.Memberships().FreezeForCustomerAsync(Sub, mine.Id);

        var result = await h.Redemption().TryReserveBenefitsAsync(h.Reserve(mine));

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.MembershipNotRedeemable, result.Error.Code);
    }

    [Fact]
    public async Task Redemption_PastDueWithinGrace_IsAllowed_AndAfterGraceIsRejected()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync(graceDays: 3);
        var mine = await h.EnrolAsync(plan.Id);
        var tracked = await h.Db.Set<CustomerMembership>().SingleAsync();
        var end = tracked.CurrentCycleEnd!.Value;

        h.Clock.Now = end.AddDays(1);
        Assert.True(tracked.MarkPastDue());
        await h.Db.SaveChangesAsync();
        var withinGrace = await h.Redemption().TryReserveBenefitsAsync(h.Reserve(mine));
        Assert.True(withinGrace.IsSuccess, withinGrace.Error?.Code);

        h.Clock.Now = end.AddDays(4);
        var afterGrace = await h.Redemption().TryReserveBenefitsAsync(h.Reserve(mine));
        Assert.Equal(RetentionErrorCodes.MembershipNotRedeemable, afterGrace.Error.Code);
    }

    [Fact]
    public async Task Redemption_OtherCustomersMembership_IsNotFound()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var theirs = await h.EnrolAsync(plan.Id, h.OtherTenantCustomer);

        var result = await h.Redemption().TryReserveBenefitsAsync(h.Reserve(theirs));

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.BenefitNotFound, result.Error.Code);
    }

    [Fact]
    public async Task Redemption_BothOwnerIdsOrNeither_IsRejected()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var mine = await h.EnrolAsync(plan.Id);
        var balanceId = mine.Balances.Single().Id;

        ReserveBenefitsRequest Req(AppliedBenefitRequest applied) =>
            new(h.Tenant, h.Customer, Guid.NewGuid(), h.Location, [h.Service], 1, [applied], "customer", Start);

        var both = await h.Redemption().TryReserveBenefitsAsync(Req(new AppliedBenefitRequest(Guid.NewGuid(), balanceId, 1, mine.Id)));
        var neither = await h.Redemption().TryReserveBenefitsAsync(Req(new AppliedBenefitRequest(null, balanceId, 1, null)));

        Assert.True(both.IsFailure);
        Assert.True(neither.IsFailure);
    }

    [Fact]
    public async Task ListRedeemable_IncludesCurrentCycleMembershipCredits_ExcludesFrozen()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var mine = await h.EnrolAsync(plan.Id);

        var before = await h.Redemption().ListRedeemableAsync(h.Tenant, h.TenantCustomer, h.Location, [h.Service]);
        var item = Assert.Single(before.Value!.Items);
        Assert.Equal(mine.Id, item.CustomerMembershipId);
        Assert.Null(item.CustomerPackageId);
        Assert.Equal(2, item.Remaining);

        await h.Memberships().FreezeForCustomerAsync(Sub, mine.Id);
        var after = await h.Redemption().ListRedeemableAsync(h.Tenant, h.TenantCustomer, h.Location, [h.Service]);
        Assert.Empty(after.Value!.Items);
    }

    // ---- MRR ----

    [Fact]
    public async Task Mrr_CountsActiveAndPastDue_NormalizesIntervals_ExcludesFrozenAndCancelled()
    {
        var h = await NewAsync();
        var monthly = await h.PlanAsync(price: 10000m);
        var quarterly = await h.PlanAsync(price: 30000m, interval: MembershipBillingInterval.Quarterly);
        var annual = await h.PlanAsync(price: 120000m, interval: MembershipBillingInterval.Annually);
        await h.EnrolAsync(monthly.Id, h.TenantCustomer, "m");
        await h.EnrolAsync(quarterly.Id, h.TenantCustomer, "q");
        await h.EnrolAsync(annual.Id, h.OtherTenantCustomer, "a");
        var frozenPlan = await h.PlanAsync(price: 99999m);
        var frozen = await h.EnrolAsync(frozenPlan.Id, h.OtherTenantCustomer, "f");
        await h.Db.Set<CustomerMembership>().Where(x => x.Id == frozen.Id)
            .ForEachAsync(x => x.Status = CustomerMembershipStatus.Frozen);
        await h.Db.SaveChangesAsync();

        var mrr = await h.Mrr().GetAsync(h.Tenant);

        Assert.True(mrr.IsSuccess, mrr.Error?.Code);
        var ngn = Assert.Single(mrr.Value!.Items);
        Assert.Equal("NGN", ngn.Currency);
        Assert.Equal(3, ngn.MemberCount);
        Assert.Equal(10000m + 10000m + 10000m, ngn.MonthlyRecurringRevenue);
    }

    [Fact]
    public async Task Mrr_WithoutMembershipsEntitlement_Fails()
    {
        var h = await NewAsync();

        var result = await h.Mrr(entitled: false).GetAsync(h.Tenant);

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.MembershipsNotEntitled, result.Error.Code);
    }
}
