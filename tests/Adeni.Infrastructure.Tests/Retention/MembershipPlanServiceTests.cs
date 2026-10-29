namespace Adeni.Infrastructure.Tests.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Retention;
using Adeni.Domain.Auditing;
using Adeni.Domain.Retention;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore;

public sealed class MembershipPlanServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly RetentionActor Manager = new("manager-1", "manager");

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
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

    private sealed class Lookup(AdeniDbContext db, Guid? inactiveService = null) : IRetentionServiceLookup
    {
        public async Task<IReadOnlyList<ServiceOfferingLookup>> FindAsync(
            Guid tenantId, IReadOnlyCollection<Guid> serviceOfferingIds, CancellationToken cancellationToken = default) =>
            await db.ServiceOfferings.Where(x => x.TenantId == tenantId && serviceOfferingIds.Contains(x.Id))
                .Select(x => new ServiceOfferingLookup(x.Id, x.Name, x.Id != inactiveService)).ToListAsync(cancellationToken);
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

        public CapturingAudit Audit { get; } = new();
        public Guid Tenant { get; } = Guid.NewGuid();
        public Guid OtherTenant { get; } = Guid.NewGuid();
        public Guid Service { get; } = Guid.NewGuid();
        public Guid OtherService { get; } = Guid.NewGuid();
        public Guid ForeignService { get; } = Guid.NewGuid();

        public MembershipPlanService Plans(bool entitled = true, Guid? inactiveService = null) =>
            new(Db, new Guard(entitled), new Lookup(Db, inactiveService), new FixedClock(), Audit);

        public async Task SeedAsync()
        {
            Db.Add(new Tenant { Id = Tenant, Name = "Salon", CreatedAt = Now });
            Db.Add(new Tenant { Id = OtherTenant, Name = "Other", CreatedAt = Now });
            Db.Add(new Domain.Booking.ServiceOffering { Id = Service, TenantId = Tenant, Name = "Haircut", PriceAmount = 5000 });
            Db.Add(new Domain.Booking.ServiceOffering { Id = OtherService, TenantId = Tenant, Name = "Color", PriceAmount = 9000 });
            Db.Add(new Domain.Booking.ServiceOffering { Id = ForeignService, TenantId = OtherTenant, Name = "Foreign", PriceAmount = 100 });
            await Db.SaveChangesAsync();
        }

        public async Task<MembershipPlanResponse> CreateAsync()
        {
            var created = await Plans().CreateAsync(Tenant, Manager, Request());
            Assert.True(created.IsSuccess, created.Error?.Code);
            return created.Value!;
        }

        public CreateMembershipPlanRequest Request() =>
            new("Fade Club", 25000m, "NGN", [new MembershipBenefitInput(Service, 2)]);
    }

    private static async Task<Harness> NewAsync()
    {
        var harness = new Harness();
        await harness.SeedAsync();
        return harness;
    }

    [Fact]
    public async Task Create_ValidRequest_PersistsPlanWithDefaultsAndAudits()
    {
        var h = await NewAsync();
        var plan = await h.CreateAsync();

        Assert.True(plan.IsActive);
        Assert.Equal("monthly", plan.BillingInterval);
        Assert.Equal("none", plan.RolloverPolicy);
        Assert.Equal("end_of_cycle", plan.CancellationPolicy);
        Assert.False(plan.AllowFreeze);
        Assert.Equal(MembershipPlan.DefaultPastDueGraceDays, plan.PastDueGraceDays);
        var benefit = Assert.Single(plan.Benefits);
        Assert.Equal("Haircut", benefit.ServiceName);
        Assert.Equal(2, benefit.QuantityPerCycle);
        Assert.Contains(h.Audit.Entries, x => x.Action == AuditActions.RetentionMembershipPlanCreated && x.EntityId == plan.Id.ToString());
    }

    [Fact]
    public async Task Create_WithTypedPolicies_RoundTripsThroughApiCodes()
    {
        var h = await NewAsync();
        var created = await h.Plans().CreateAsync(h.Tenant, Manager, new CreateMembershipPlanRequest(
            "Annual", 240000m, "ngn", [new MembershipBenefitInput(h.Service, 24)],
            BillingInterval: "annually", RolloverPolicy: "carry_over_one_cycle",
            CancellationPolicy: "immediate", AllowFreeze: true, PastDueGraceDays: 0));

        Assert.True(created.IsSuccess, created.Error?.Code);
        Assert.Equal("annually", created.Value!.BillingInterval);
        Assert.Equal("carry_over_one_cycle", created.Value.RolloverPolicy);
        Assert.Equal("immediate", created.Value.CancellationPolicy);
        Assert.True(created.Value.AllowFreeze);
        Assert.Equal(0, created.Value.PastDueGraceDays);
        Assert.Equal("NGN", created.Value.Currency);
    }

    [Theory]
    [InlineData("", 100, "NGN", "monthly", 7)]
    [InlineData("Plan", 0, "NGN", "monthly", 7)]
    [InlineData("Plan", -5, "NGN", "monthly", 7)]
    [InlineData("Plan", 10.123, "NGN", "monthly", 7)]
    [InlineData("Plan", 100, "EUR", "monthly", 7)]
    [InlineData("Plan", 100, "NGN", "weekly", 7)]
    [InlineData("Plan", 100, "NGN", "monthly", 31)]
    [InlineData("Plan", 100, "NGN", "monthly", -1)]
    public async Task Create_InvalidField_FailsValidation(string name, double price, string currency, string interval, int grace)
    {
        var h = await NewAsync();
        var result = await h.Plans().CreateAsync(h.Tenant, Manager, new CreateMembershipPlanRequest(
            name, (decimal)price, currency, [new MembershipBenefitInput(h.Service, 1)],
            BillingInterval: interval, PastDueGraceDays: grace));

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.Validation, result.Error!.Code);
        Assert.Empty(h.Db.Set<MembershipPlan>());
    }

    [Fact]
    public async Task Create_BenefitProblems_FailValidation()
    {
        var h = await NewAsync();
        var plans = h.Plans();

        var empty = await plans.CreateAsync(h.Tenant, Manager, new("P", 100m, "NGN", []));
        var zero = await plans.CreateAsync(h.Tenant, Manager, new("P", 100m, "NGN", [new(h.Service, 0)]));
        var duplicate = await plans.CreateAsync(h.Tenant, Manager, new("P", 100m, "NGN", [new(h.Service, 1), new(h.Service, 2)]));
        var foreign = await plans.CreateAsync(h.Tenant, Manager, new("P", 100m, "NGN", [new(h.ForeignService, 1)]));
        var inactive = await h.Plans(inactiveService: h.Service)
            .CreateAsync(h.Tenant, Manager, new("P", 100m, "NGN", [new(h.Service, 1)]));

        foreach (var result in new[] { empty, zero, duplicate, foreign, inactive })
        {
            Assert.True(result.IsFailure);
            Assert.Equal(RetentionErrorCodes.Validation, result.Error!.Code);
        }
    }

    [Fact]
    public async Task Create_NotEntitled_ReturnsMembershipsEntitlementError()
    {
        var h = await NewAsync();
        var result = await h.Plans(entitled: false).CreateAsync(h.Tenant, Manager, h.Request());

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.MembershipsNotEntitled, result.Error!.Code);
        Assert.Empty(h.Db.Set<MembershipPlan>());
    }

    [Fact]
    public async Task ReadsAndMutations_NotEntitled_AllDenied()
    {
        var h = await NewAsync();
        var plan = await h.CreateAsync();
        var denied = h.Plans(entitled: false);

        var results = new[]
        {
            (await denied.ListAsync(h.Tenant, false)).Error,
            (await denied.GetAsync(h.Tenant, plan.Id)).Error,
            (await denied.UpdateAsync(h.Tenant, Manager, plan.Id, new UpdateMembershipPlanRequest(Name: "x"))).Error,
            (await denied.DeactivateAsync(h.Tenant, Manager, plan.Id)).Error,
        };

        Assert.All(results, error => Assert.Equal(RetentionErrorCodes.MembershipsNotEntitled, error!.Code));
    }

    [Fact]
    public async Task Update_PartialPatch_KeepsUnspecifiedFieldsAndReplacesBenefits()
    {
        var h = await NewAsync();
        var plan = await h.CreateAsync();

        var updated = await h.Plans().UpdateAsync(h.Tenant, Manager, plan.Id, new UpdateMembershipPlanRequest(
            Price: 30000m, AllowFreeze: true, Benefits: [new MembershipBenefitInput(h.OtherService, 1)]));

        Assert.True(updated.IsSuccess, updated.Error?.Code);
        Assert.Equal("Fade Club", updated.Value!.Name);
        Assert.Equal(30000m, updated.Value.Price);
        Assert.True(updated.Value.AllowFreeze);
        var benefit = Assert.Single(updated.Value.Benefits);
        Assert.Equal(h.OtherService, benefit.ServiceOfferingId);
        Assert.Equal(1, await h.Db.Set<MembershipPlanBenefit>().CountAsync());
        Assert.Contains(h.Audit.Entries, x => x.Action == AuditActions.RetentionMembershipPlanUpdated);
    }

    [Fact]
    public async Task Deactivate_IsIdempotentAndHiddenFromActiveOnlyList()
    {
        var h = await NewAsync();
        var plan = await h.CreateAsync();

        var first = await h.Plans().DeactivateAsync(h.Tenant, Manager, plan.Id);
        var second = await h.Plans().DeactivateAsync(h.Tenant, Manager, plan.Id);
        var active = await h.Plans().ListAsync(h.Tenant, activeOnly: true);
        var all = await h.Plans().ListAsync(h.Tenant, activeOnly: false);

        Assert.False(first.Value!.IsActive);
        Assert.False(second.Value!.IsActive);
        Assert.Empty(active.Value!.Items);
        Assert.Single(all.Value!.Items);
        Assert.Single(h.Audit.Entries, x => x.Action == AuditActions.RetentionMembershipPlanDeactivated);
    }

    [Fact]
    public async Task CrossTenant_GetUpdateDeactivate_ReturnNotFoundAndDoNotLeak()
    {
        var h = await NewAsync();
        var plan = await h.CreateAsync();
        var plans = h.Plans();

        var get = await plans.GetAsync(h.OtherTenant, plan.Id);
        var update = await plans.UpdateAsync(h.OtherTenant, Manager, plan.Id, new UpdateMembershipPlanRequest(Name: "hijack"));
        var deactivate = await plans.DeactivateAsync(h.OtherTenant, Manager, plan.Id);
        var list = await plans.ListAsync(h.OtherTenant, false);

        Assert.Equal(RetentionErrorCodes.MembershipPlanNotFound, get.Error!.Code);
        Assert.Equal(RetentionErrorCodes.MembershipPlanNotFound, update.Error!.Code);
        Assert.Equal(RetentionErrorCodes.MembershipPlanNotFound, deactivate.Error!.Code);
        Assert.Empty(list.Value!.Items);
        var stored = await h.Db.Set<MembershipPlan>().AsNoTracking().SingleAsync();
        Assert.Equal("Fade Club", stored.Name);
        Assert.True(stored.IsActive);
    }
}
