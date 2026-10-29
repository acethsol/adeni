namespace Adeni.Infrastructure.Tests.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Retention;
using Adeni.Domain.Auditing;
using Adeni.Domain.Retention;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore;

public sealed class CustomerMembershipServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly RetentionActor Owner = new("owner-1", "owner");
    private static readonly RetentionActor Manager = new("manager-1", "manager");

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Start;
    }

    private sealed class Guard(bool entitled) : IRetentionEntitlementGuard
    {
        public Task<bool> IsLoyaltyBasicEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Domain.Common.Result> EnsureLoyaltyBasicEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());

        public Task<bool> IsMembershipsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(entitled);

        public Task<Domain.Common.Result> EnsureMembershipsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(entitled
                ? Domain.Common.Result.Success()
                : Domain.Common.Result.Failure(RetentionErrorCodes.MembershipsNotEntitledError()));

        public Task<bool> IsRecurringBillingEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Domain.Common.Result> EnsureRecurringBillingEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());

        public Task<bool> IsPackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Domain.Common.Result> EnsurePackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());
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
        public CapturingAudit Audit { get; } = new();
        public Guid Tenant { get; } = Guid.NewGuid();
        public Guid OtherTenant { get; } = Guid.NewGuid();
        public Guid Location { get; } = Guid.NewGuid();
        public Guid Customer { get; } = Guid.NewGuid();
        public Guid OtherTenantCustomer { get; } = Guid.NewGuid();
        public Guid Service { get; } = Guid.NewGuid();
        public Guid OtherService { get; } = Guid.NewGuid();

        public CustomerMembershipService Memberships(bool entitled = true) =>
            new(Db, new Guard(entitled), Events, new FixedClock(), Audit);

        public async Task SeedAsync()
        {
            Db.Add(new Tenant { Id = Tenant, Name = "Salon", CreatedAt = Start });
            Db.Add(new Tenant { Id = OtherTenant, Name = "Other", CreatedAt = Start });
            Db.Add(new BusinessLocation { Id = Location, TenantId = Tenant, Name = "A", Slug = "a", IsPrimary = true, CreatedAt = Start });
            Db.Add(new TenantCustomer
            {
                Id = Customer, TenantId = Tenant, CustomerId = Guid.NewGuid(), DisplayName = "Ada",
                CreatedAt = Start, UpdatedAt = Start, LastSeenAt = Start,
            });
            Db.Add(new TenantCustomer
            {
                Id = OtherTenantCustomer, TenantId = OtherTenant, CustomerId = Guid.NewGuid(), DisplayName = "Bo",
                CreatedAt = Start, UpdatedAt = Start, LastSeenAt = Start,
            });
            Db.Add(new Domain.Booking.ServiceOffering { Id = Service, TenantId = Tenant, Name = "Haircut", PriceAmount = 5000 });
            Db.Add(new Domain.Booking.ServiceOffering { Id = OtherService, TenantId = Tenant, Name = "Color", PriceAmount = 9000 });
            await Db.SaveChangesAsync();
        }

        public async Task<MembershipPlan> PlanAsync(
            bool allowFreeze = true,
            MembershipCancellationPolicy cancellation = MembershipCancellationPolicy.EndOfCycle,
            MembershipBillingInterval interval = MembershipBillingInterval.Monthly,
            bool active = true,
            Guid? tenant = null)
        {
            var tenantId = tenant ?? Tenant;
            var plan = new MembershipPlan
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Fade Club", Price = 25000m, Currency = "NGN",
                BillingInterval = interval, CancellationPolicy = cancellation, AllowFreeze = allowFreeze,
                IsActive = active, CreatedAt = Start, UpdatedAt = Start,
            };
            plan.Benefits.Add(new MembershipPlanBenefit
            {
                Id = Guid.NewGuid(), TenantId = tenantId, MembershipPlanId = plan.Id, ServiceOfferingId = Service,
                ServiceName = "Haircut", QuantityPerCycle = 2, SortOrder = 0,
            });
            plan.Benefits.Add(new MembershipPlanBenefit
            {
                Id = Guid.NewGuid(), TenantId = tenantId, MembershipPlanId = plan.Id, ServiceOfferingId = OtherService,
                ServiceName = "Color", QuantityPerCycle = 1, SortOrder = 1,
            });
            Db.Add(plan);
            await Db.SaveChangesAsync();
            return plan;
        }

        public EnrolMembershipRequest Enrol(Guid planId, string method = "till", string? note = null) =>
            new(planId, Location, method, note);

        public async Task<CustomerMembershipResponse> EnrolOkAsync(Guid planId, string key = "k1")
        {
            var result = await Memberships().EnrolAsync(Tenant, Manager, Customer, Enrol(planId), key);
            Assert.True(result.IsSuccess, result.Error?.Code);
            return result.Value!;
        }
    }

    private static async Task<Harness> NewAsync()
    {
        var h = new Harness();
        await h.SeedAsync();
        return h;
    }

    [Fact]
    public async Task Enrol_AtTill_ActivatesAndGrantsFirstCycleCredits()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();

        var membership = await h.EnrolOkAsync(plan.Id);

        Assert.Equal("active", membership.Status);
        Assert.Equal("in_person_till", membership.EnrolChannel);
        Assert.Equal(1, membership.CurrentCycleNumber);
        Assert.Equal(Start, membership.CurrentCycleStart);
        Assert.Equal(Start.AddMonths(1), membership.CurrentCycleEnd);
        Assert.Equal(25000m, membership.Price);
        Assert.Equal(2, membership.Benefits.Count);
        Assert.Equal(new[] { 1, 2 }, membership.Balances.Select(x => x.Granted).Order().ToArray());
        Assert.All(membership.Balances, b =>
        {
            Assert.Equal(1, b.CycleNumber);
            Assert.Equal(b.Granted, b.Remaining);
            Assert.Equal(membership.CurrentCycleEnd, b.ExpiresAt);
        });

        var ledger = await h.Db.Set<BenefitTransaction>().ToListAsync();
        Assert.Equal(2, ledger.Count);
        Assert.All(ledger, t => Assert.Equal(BenefitTransactionKind.Grant, t.Kind));
        Assert.Contains(h.Audit.Entries, e => e.Action == AuditActions.RetentionMembershipEnrolled);
    }

    [Theory]
    [InlineData(MembershipBillingInterval.Quarterly, 3)]
    [InlineData(MembershipBillingInterval.Annually, 12)]
    public async Task Enrol_QuarterlyOrAnnual_CycleEndFollowsInterval(MembershipBillingInterval interval, int months)
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync(interval: interval);

        var membership = await h.EnrolOkAsync(plan.Id);

        Assert.Equal(Start.AddMonths(months), membership.CurrentCycleEnd);
    }

    [Fact]
    public async Task Enrol_SameKeyAndBody_ReplaysWithoutDuplicatingCredits()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();

        var first = await h.EnrolOkAsync(plan.Id, "dup");
        var second = await h.Memberships().EnrolAsync(h.Tenant, Manager, h.Customer, h.Enrol(plan.Id), "dup");

        Assert.True(second.IsSuccess, second.Error?.Code);
        Assert.Equal(first.Id, second.Value!.Id);
        Assert.Equal(1, await h.Db.Set<CustomerMembership>().CountAsync());
        Assert.Equal(2, await h.Db.Set<BenefitBalance>().CountAsync());
        Assert.Equal(2, await h.Db.Set<BenefitTransaction>().CountAsync());
    }

    [Fact]
    public async Task Enrol_SameKeyDifferentBody_ReturnsIdempotencyConflict()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        await h.EnrolOkAsync(plan.Id, "dup");

        var conflict = await h.Memberships().EnrolAsync(h.Tenant, Manager, h.Customer, h.Enrol(plan.Id, "transfer"), "dup");

        Assert.True(conflict.IsFailure);
        Assert.Equal(RetentionErrorCodes.IdempotencyConflict, conflict.Error.Code);
    }

    [Fact]
    public async Task Enrol_WithoutIdempotencyKey_Fails()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();

        var result = await h.Memberships().EnrolAsync(h.Tenant, Manager, h.Customer, h.Enrol(plan.Id), null);

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.IdempotencyKeyRequired, result.Error.Code);
    }

    [Fact]
    public async Task Enrol_SecondLiveMembershipOnSamePlan_Fails()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        await h.EnrolOkAsync(plan.Id, "one");

        var again = await h.Memberships().EnrolAsync(h.Tenant, Manager, h.Customer, h.Enrol(plan.Id), "two");

        Assert.True(again.IsFailure);
        Assert.Equal(RetentionErrorCodes.MembershipAlreadyActive, again.Error.Code);
        Assert.Equal(1, await h.Db.Set<CustomerMembership>().CountAsync());
    }

    [Fact]
    public async Task Enrol_AfterCancelled_IsAllowedAgain()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync(cancellation: MembershipCancellationPolicy.Immediate);
        var first = await h.EnrolOkAsync(plan.Id, "one");
        Assert.True((await h.Memberships().CancelAsync(h.Tenant, Owner, first.Id, new CancelMembershipRequest())).IsSuccess);

        var again = await h.Memberships().EnrolAsync(h.Tenant, Manager, h.Customer, h.Enrol(plan.Id), "two");

        Assert.True(again.IsSuccess, again.Error?.Code);
        Assert.NotEqual(first.Id, again.Value!.Id);
    }

    [Fact]
    public async Task Enrol_InactivePlan_Fails()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync(active: false);

        var result = await h.Memberships().EnrolAsync(h.Tenant, Manager, h.Customer, h.Enrol(plan.Id), "k");

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.MembershipPlanInactive, result.Error.Code);
    }

    [Fact]
    public async Task Enrol_PlanFromOtherTenant_IsNotFound()
    {
        var h = await NewAsync();
        var foreign = await h.PlanAsync(tenant: h.OtherTenant);

        var result = await h.Memberships().EnrolAsync(h.Tenant, Manager, h.Customer, h.Enrol(foreign.Id), "k");

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.MembershipPlanNotFound, result.Error.Code);
    }

    [Fact]
    public async Task Enrol_CustomerFromOtherTenant_IsNotFound()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();

        var result = await h.Memberships().EnrolAsync(h.Tenant, Manager, h.OtherTenantCustomer, h.Enrol(plan.Id), "k");

        Assert.True(result.IsFailure);
        Assert.Equal(0, await h.Db.Set<CustomerMembership>().CountAsync());
    }

    [Theory]
    [InlineData("card")]
    [InlineData("")]
    public async Task Enrol_InvalidPaymentMethod_FailsValidation(string method)
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();

        var result = await h.Memberships().EnrolAsync(h.Tenant, Manager, h.Customer, h.Enrol(plan.Id, method), "k");

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.Validation, result.Error.Code);
    }

    [Fact]
    public async Task Enrol_NotEntitled_IsDenied()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();

        var result = await h.Memberships(entitled: false).EnrolAsync(h.Tenant, Manager, h.Customer, h.Enrol(plan.Id), "k");

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.MembershipsNotEntitled, result.Error.Code);
    }

    [Fact]
    public async Task Enrol_SnapshotsTerms_LaterPlanEditsDoNotRewriteMembership()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var membership = await h.EnrolOkAsync(plan.Id);

        var tracked = await h.Db.Set<MembershipPlan>().SingleAsync(x => x.Id == plan.Id);
        tracked.Price = 99999m;
        tracked.Name = "Renamed";
        await h.Db.SaveChangesAsync();

        var reloaded = await h.Memberships().GetAsync(h.Tenant, membership.Id);
        Assert.Equal(25000m, reloaded.Value!.Price);
        Assert.Equal("Fade Club", reloaded.Value.Name);
    }

    [Fact]
    public async Task List_ReturnsOnlyThisCustomersMemberships_AndRejectsForeignCustomer()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        await h.EnrolOkAsync(plan.Id);

        var list = await h.Memberships().ListForCustomerAsync(h.Tenant, h.Customer);
        var foreign = await h.Memberships().ListForCustomerAsync(h.Tenant, h.OtherTenantCustomer);

        Assert.Single(list.Value!.Items);
        Assert.True(foreign.IsFailure);
    }

    [Fact]
    public async Task Get_FromOtherTenant_IsNotFound()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var membership = await h.EnrolOkAsync(plan.Id);

        var result = await h.Memberships().GetAsync(h.OtherTenant, membership.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.MembershipNotFound, result.Error.Code);
    }

    [Fact]
    public async Task Freeze_ThenUnfreeze_RoundTripsAndIsIdempotent()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var membership = await h.EnrolOkAsync(plan.Id);
        var service = h.Memberships();

        var frozen = await service.FreezeAsync(h.Tenant, Owner, membership.Id);
        var frozenAgain = await service.FreezeAsync(h.Tenant, Owner, membership.Id);
        var thawed = await service.UnfreezeAsync(h.Tenant, Owner, membership.Id);
        var thawedAgain = await service.UnfreezeAsync(h.Tenant, Owner, membership.Id);

        Assert.Equal("frozen", frozen.Value!.Status);
        Assert.Equal("frozen", frozenAgain.Value!.Status);
        Assert.Equal("active", thawed.Value!.Status);
        Assert.Equal("active", thawedAgain.Value!.Status);
        Assert.Single(h.Audit.Entries, e => e.Action == AuditActions.RetentionMembershipFrozen);
        Assert.Single(h.Audit.Entries, e => e.Action == AuditActions.RetentionMembershipUnfrozen);
    }

    [Fact]
    public async Task Freeze_WhenPlanDisallows_Fails()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync(allowFreeze: false);
        var membership = await h.EnrolOkAsync(plan.Id);

        var result = await h.Memberships().FreezeAsync(h.Tenant, Owner, membership.Id);

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.MembershipFreezeNotAllowed, result.Error.Code);
    }

    [Fact]
    public async Task FreezeAndCancel_NonOwner_AreDenied()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var membership = await h.EnrolOkAsync(plan.Id);

        var freeze = await h.Memberships().FreezeAsync(h.Tenant, Manager, membership.Id);
        var cancel = await h.Memberships().CancelAsync(h.Tenant, Manager, membership.Id, new CancelMembershipRequest());

        Assert.Equal(RetentionErrorCodes.OwnerRequired, freeze.Error.Code);
        Assert.Equal(RetentionErrorCodes.OwnerRequired, cancel.Error.Code);
    }

    [Fact]
    public async Task Cancel_EndOfCyclePolicy_KeepsCreditsUntilCycleEnd()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync(cancellation: MembershipCancellationPolicy.EndOfCycle);
        var membership = await h.EnrolOkAsync(plan.Id);

        var cancelled = await h.Memberships().CancelAsync(h.Tenant, Owner, membership.Id, new CancelMembershipRequest("moving"));
        var again = await h.Memberships().CancelAsync(h.Tenant, Owner, membership.Id, new CancelMembershipRequest());

        Assert.Equal("cancel_pending", cancelled.Value!.Status);
        Assert.Equal("moving", cancelled.Value.CancelReason);
        Assert.Equal(3, cancelled.Value.Balances.Sum(x => x.Remaining));
        Assert.Null(cancelled.Value.CancelledAt);
        Assert.Equal("cancel_pending", again.Value!.Status);
        Assert.Single(h.Audit.Entries, e => e.Action == AuditActions.RetentionMembershipCancelled);
    }

    [Fact]
    public async Task Cancel_ImmediatePolicy_EndsNowAndForfeitsRemainingCredits()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync(cancellation: MembershipCancellationPolicy.Immediate);
        var membership = await h.EnrolOkAsync(plan.Id);

        var cancelled = await h.Memberships().CancelAsync(h.Tenant, Owner, membership.Id, new CancelMembershipRequest());

        Assert.Equal("cancelled", cancelled.Value!.Status);
        Assert.NotNull(cancelled.Value.CancelledAt);
        Assert.All(cancelled.Value.Balances, b =>
        {
            Assert.Equal(0, b.Remaining);
            Assert.Equal(b.Granted, b.Forfeited);
        });
        var kinds = await h.Db.Set<BenefitTransaction>().Select(x => x.Kind).ToListAsync();
        Assert.Equal(2, kinds.Count(k => k == BenefitTransactionKind.Forfeit));
    }

    [Fact]
    public async Task Cancel_AfterCancelled_FreezeIsInvalidState()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync(cancellation: MembershipCancellationPolicy.Immediate);
        var membership = await h.EnrolOkAsync(plan.Id);
        await h.Memberships().CancelAsync(h.Tenant, Owner, membership.Id, new CancelMembershipRequest());

        var freeze = await h.Memberships().FreezeAsync(h.Tenant, Owner, membership.Id);

        Assert.True(freeze.IsFailure);
        Assert.Equal(RetentionErrorCodes.MembershipInvalidState, freeze.Error.Code);
    }

    [Fact]
    public async Task Cancel_FrozenMembership_IsAllowed()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync(cancellation: MembershipCancellationPolicy.Immediate);
        var membership = await h.EnrolOkAsync(plan.Id);
        await h.Memberships().FreezeAsync(h.Tenant, Owner, membership.Id);

        var cancelled = await h.Memberships().CancelAsync(h.Tenant, Owner, membership.Id, new CancelMembershipRequest());

        Assert.Equal("cancelled", cancelled.Value!.Status);
    }

    [Fact]
    public async Task Cancel_ReasonTooLong_FailsValidation()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var membership = await h.EnrolOkAsync(plan.Id);

        var result = await h.Memberships().CancelAsync(
            h.Tenant, Owner, membership.Id, new CancelMembershipRequest(new string('x', 501)));

        Assert.Equal(RetentionErrorCodes.Validation, result.Error.Code);
    }

    [Fact]
    public async Task Transitions_OtherTenantMembership_AreNotFound()
    {
        var h = await NewAsync();
        var plan = await h.PlanAsync();
        var membership = await h.EnrolOkAsync(plan.Id);

        var freeze = await h.Memberships().FreezeAsync(h.OtherTenant, Owner, membership.Id);
        var cancel = await h.Memberships().CancelAsync(h.OtherTenant, Owner, membership.Id, new CancelMembershipRequest());

        Assert.Equal(RetentionErrorCodes.MembershipNotFound, freeze.Error.Code);
        Assert.Equal(RetentionErrorCodes.MembershipNotFound, cancel.Error.Code);
    }
}
