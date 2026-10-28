namespace Adeni.Infrastructure.Tests.Retention;

using Adeni.Application.Retention;
using Adeni.Domain.Retention;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore;

public sealed class RetentionServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly RetentionActor Owner = new("owner-1", "owner");
    private static readonly RetentionActor Manager = new("manager-1", "manager");

    private sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = Start;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Guard(bool entitled = true) : IRetentionEntitlementGuard
    {
        public Task<bool> IsPackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(entitled);

        public Task<Domain.Common.Result> EnsurePackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(entitled
                ? Domain.Common.Result.Success()
                : Domain.Common.Result.Failure(RetentionErrorCodes.PackagesNotEntitledError()));
    }

    private sealed class Lookup(AdeniDbContext db) : IRetentionServiceLookup
    {
        public async Task<IReadOnlyList<ServiceOfferingLookup>> FindAsync(
            Guid tenantId, IReadOnlyCollection<Guid> serviceOfferingIds, CancellationToken cancellationToken = default) =>
            await db.ServiceOfferings.Where(x => x.TenantId == tenantId && serviceOfferingIds.Contains(x.Id))
                .Select(x => new ServiceOfferingLookup(x.Id, x.Name, true)).ToListAsync(cancellationToken);
    }

    private sealed class Harness
    {
        public AdeniDbContext Db { get; } = new(
            new DbContextOptionsBuilder<AdeniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new TenantContext());

        public MutableClock Clock { get; } = new();
        public DomainEventCollector Events { get; } = new();
        public Guid Tenant { get; } = Guid.NewGuid();
        public Guid LocationA { get; } = Guid.NewGuid();
        public Guid LocationB { get; } = Guid.NewGuid();
        public Guid PlatformCustomer { get; } = Guid.NewGuid();
        public Guid TenantCustomer { get; } = Guid.NewGuid();
        public Guid Service { get; } = Guid.NewGuid();
        public Guid OtherService { get; } = Guid.NewGuid();

        public PackageCatalogService Catalog(bool entitled = true) =>
            new(Db, new Guard(entitled), new Lookup(Db), Clock);

        public CustomerPackageService Packages(bool entitled = true) =>
            new(Db, new Guard(entitled), Events, Clock);

        public BenefitRedemptionService Benefits() => new(Db, Events, Clock);

        public PackageExpiryService Expiry() =>
            new(Db, Events, Clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<PackageExpiryService>.Instance);

        public async Task SeedAsync()
        {
            Db.Add(new Tenant { Id = Tenant, Name = "Salon", CreatedAt = Start });
            Db.Add(new BusinessLocation { Id = LocationA, TenantId = Tenant, Name = "A", Slug = "a", IsPrimary = true, CreatedAt = Start });
            Db.Add(new BusinessLocation { Id = LocationB, TenantId = Tenant, Name = "B", Slug = "b", CreatedAt = Start });
            Db.Add(new TenantCustomer
            {
                Id = TenantCustomer, TenantId = Tenant, CustomerId = PlatformCustomer, DisplayName = "Ada",
                CreatedAt = Start, UpdatedAt = Start, LastSeenAt = Start,
            });
            Db.Add(new Domain.Booking.ServiceOffering { Id = Service, TenantId = Tenant, Name = "Haircut", PriceAmount = 5000 });
            Db.Add(new Domain.Booking.ServiceOffering { Id = OtherService, TenantId = Tenant, Name = "Color", PriceAmount = 9000 });
            await Db.SaveChangesAsync();
        }

        public async Task<PackageDefinitionResponse> DefinitionAsync(
            int quantity = 3, string? redemptionScope = null, string? sellScope = null,
            IReadOnlyList<Guid>? redemptionLocations = null, IReadOnlyList<Guid>? sellLocations = null)
        {
            var created = await Catalog().CreateAsync(Tenant, Owner, new CreatePackageDefinitionRequest(
                "Cut x3", 12000m, "NGN", [new PackageBenefitInput(Service, quantity)], null, null,
                redemptionScope, sellScope, redemptionLocations, sellLocations));
            Assert.True(created.IsSuccess, created.Error?.Code);
            return created.Value!;
        }

        public async Task<CustomerPackageResponse> SellAsync(PackageDefinitionResponse definition, string key = "sell-1", Guid? location = null)
        {
            var sold = await Packages().SellAsync(
                Tenant, Manager, TenantCustomer,
                new SellPackageRequest(definition.Id, location ?? LocationA, SellPaymentMethods.Till), key);
            Assert.True(sold.IsSuccess, sold.Error?.Code);
            return sold.Value!;
        }

        public ReserveBenefitsRequest Reserve(
            CustomerPackageResponse package, Guid bookingId, Guid? location = null, int quantity = 1, Guid? service = null) =>
            new(Tenant, PlatformCustomer, bookingId, location ?? LocationA, [service ?? Service], 1,
                [new AppliedBenefitRequest(package.Id, package.Balances[0].Id, quantity)], "customer", Clock.Now);

        public Task<BenefitBalance> BalanceAsync(CustomerPackageResponse package) =>
            Db.Set<BenefitBalance>().AsNoTracking().SingleAsync(x => x.Id == package.Balances[0].Id);
    }

    private static async Task<Harness> NewAsync()
    {
        var harness = new Harness();
        await harness.SeedAsync();
        return harness;
    }

    // ---- Sell ----

    [Fact]
    public async Task Sell_ActiveDefinition_GrantsBalancesWithTwelveMonthExpiry()
    {
        var h = await NewAsync();
        var sold = await h.SellAsync(await h.DefinitionAsync(quantity: 4));

        Assert.Equal("active", sold.Status);
        Assert.Equal(4, sold.Balances[0].Granted);
        Assert.Equal(4, sold.Balances[0].Remaining);
        Assert.Equal(h.Clock.Now.AddMonths(12), sold.ExpiresAt);
        Assert.Equal(1, await h.Db.Set<BenefitTransaction>().CountAsync(x => x.Kind == BenefitTransactionKind.Grant));
    }

    [Fact]
    public async Task Sell_SameKeyAndBody_ReplaysWithoutDoubleGrant()
    {
        var h = await NewAsync();
        var definition = await h.DefinitionAsync();
        var first = await h.SellAsync(definition, "same-key");
        var second = await h.SellAsync(definition, "same-key");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await h.Db.Set<CustomerPackage>().CountAsync());
        Assert.Equal(1, await h.Db.Set<BenefitTransaction>().CountAsync(x => x.Kind == BenefitTransactionKind.Grant));
    }

    [Fact]
    public async Task Sell_SameKeyDifferentBody_IsIdempotencyConflict()
    {
        var h = await NewAsync();
        var definition = await h.DefinitionAsync();
        await h.SellAsync(definition, "k");

        var result = await h.Packages().SellAsync(
            h.Tenant, Manager, h.TenantCustomer,
            new SellPackageRequest(definition.Id, h.LocationB, SellPaymentMethods.Till), "k");

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.IdempotencyConflict, result.Error!.Code);
    }

    [Fact]
    public async Task Sell_MissingIdempotencyKey_IsRejected()
    {
        var h = await NewAsync();
        var definition = await h.DefinitionAsync();

        var result = await h.Packages().SellAsync(
            h.Tenant, Manager, h.TenantCustomer,
            new SellPackageRequest(definition.Id, h.LocationA, SellPaymentMethods.Till), null);

        Assert.Equal(RetentionErrorCodes.IdempotencyKeyRequired, result.Error!.Code);
    }

    [Fact]
    public async Task Sell_NotEntitled_IsDenied()
    {
        var h = await NewAsync();
        var definition = await h.DefinitionAsync();

        var result = await h.Packages(entitled: false).SellAsync(
            h.Tenant, Manager, h.TenantCustomer,
            new SellPackageRequest(definition.Id, h.LocationA, SellPaymentMethods.Till), "k");

        Assert.Equal(RetentionErrorCodes.PackagesNotEntitled, result.Error!.Code);
    }

    [Fact]
    public async Task Sell_SelectedSellScope_DeniesOtherLocation()
    {
        var h = await NewAsync();
        var definition = await h.DefinitionAsync(sellScope: RetentionScopeCodes.SelectedLocations, sellLocations: [h.LocationA]);

        var result = await h.Packages().SellAsync(
            h.Tenant, Manager, h.TenantCustomer,
            new SellPackageRequest(definition.Id, h.LocationB, SellPaymentMethods.Transfer), "k");

        Assert.Equal(RetentionErrorCodes.SellLocationDenied, result.Error!.Code);
    }

    [Fact]
    public async Task Sell_InactiveDefinition_IsRejected()
    {
        var h = await NewAsync();
        var definition = await h.DefinitionAsync();
        Assert.True((await h.Catalog().DeactivateAsync(h.Tenant, Owner, definition.Id)).IsSuccess);

        var result = await h.Packages().SellAsync(
            h.Tenant, Manager, h.TenantCustomer,
            new SellPackageRequest(definition.Id, h.LocationA, SellPaymentMethods.Till), "k");

        Assert.Equal(RetentionErrorCodes.PackageInactive, result.Error!.Code);
    }

    [Fact]
    public async Task Sell_CrossTenantCustomer_IsNotFound()
    {
        var h = await NewAsync();
        var definition = await h.DefinitionAsync();

        var result = await h.Packages().SellAsync(
            Guid.NewGuid(), Manager, h.TenantCustomer,
            new SellPackageRequest(definition.Id, h.LocationA, SellPaymentMethods.Till), "k");

        Assert.True(result.IsFailure);
        Assert.NotEqual(RetentionErrorCodes.IdempotencyConflict, result.Error!.Code);
        Assert.Empty(await h.Db.Set<CustomerPackage>().ToListAsync());
    }

    [Fact]
    public async Task Catalog_CreateWithForeignTenantService_IsRejected()
    {
        var h = await NewAsync();

        var result = await h.Catalog().CreateAsync(h.Tenant, Owner, new CreatePackageDefinitionRequest(
            "Bad", 100m, "NGN", [new PackageBenefitInput(Guid.NewGuid(), 1)]));

        Assert.True(result.IsFailure);
    }

    // ---- Reserve / release / finalize ----

    [Fact]
    public async Task Reserve_Release_Finalize_FollowLedgerMath()
    {
        var h = await NewAsync();
        var sold = await h.SellAsync(await h.DefinitionAsync(quantity: 2));
        var booking = Guid.NewGuid();

        var reserved = await h.Benefits().TryReserveBenefitsAsync(h.Reserve(sold, booking));
        Assert.True(reserved.IsSuccess, reserved.Error?.Code);
        await h.Db.SaveChangesAsync();
        var afterReserve = await h.BalanceAsync(sold);
        Assert.Equal(1, afterReserve.Reserved);
        Assert.Equal(1, afterReserve.Remaining);

        Assert.True((await h.Benefits().FinalizeRedemptionsAsync(h.Tenant, booking, "staff")).IsSuccess);
        await h.Db.SaveChangesAsync();
        var afterFinalize = await h.BalanceAsync(sold);
        Assert.Equal(0, afterFinalize.Reserved);
        Assert.Equal(1, afterFinalize.Used);
        Assert.Equal(1, afterFinalize.Remaining);
    }

    [Fact]
    public async Task Release_RestoresCredit_AndSecondReleaseIsNoop()
    {
        var h = await NewAsync();
        var sold = await h.SellAsync(await h.DefinitionAsync(quantity: 1));
        var booking = Guid.NewGuid();
        Assert.True((await h.Benefits().TryReserveBenefitsAsync(h.Reserve(sold, booking))).IsSuccess);
        await h.Db.SaveChangesAsync();

        Assert.True((await h.Benefits().ReleaseReservationsAsync(h.Tenant, booking, "customer", "cancelled")).IsSuccess);
        await h.Db.SaveChangesAsync();
        Assert.True((await h.Benefits().ReleaseReservationsAsync(h.Tenant, booking, "customer", "cancelled")).IsSuccess);
        await h.Db.SaveChangesAsync();

        var balance = await h.BalanceAsync(sold);
        Assert.Equal(1, balance.Remaining);
        Assert.Equal(0, balance.Reserved);
        Assert.Equal(1, await h.Db.Set<BenefitTransaction>().CountAsync(x => x.Kind == BenefitTransactionKind.Release));
    }

    [Fact]
    public async Task Reserve_SecondBookingBeyondRemaining_IsInsufficient()
    {
        var h = await NewAsync();
        var sold = await h.SellAsync(await h.DefinitionAsync(quantity: 1));
        Assert.True((await h.Benefits().TryReserveBenefitsAsync(h.Reserve(sold, Guid.NewGuid()))).IsSuccess);
        await h.Db.SaveChangesAsync();

        var second = await h.Benefits().TryReserveBenefitsAsync(h.Reserve(sold, Guid.NewGuid()));

        Assert.Equal(RetentionErrorCodes.BenefitInsufficient, second.Error!.Code);
    }

    [Fact]
    public async Task Reserve_SameBookingTwice_IsIdempotent()
    {
        var h = await NewAsync();
        var sold = await h.SellAsync(await h.DefinitionAsync(quantity: 2));
        var booking = Guid.NewGuid();
        Assert.True((await h.Benefits().TryReserveBenefitsAsync(h.Reserve(sold, booking))).IsSuccess);
        await h.Db.SaveChangesAsync();

        Assert.True((await h.Benefits().TryReserveBenefitsAsync(h.Reserve(sold, booking))).IsSuccess);
        await h.Db.SaveChangesAsync();

        Assert.Equal(1, (await h.BalanceAsync(sold)).Reserved);
    }

    [Fact]
    public async Task Reserve_ServiceNotInBooking_IsMismatch()
    {
        var h = await NewAsync();
        var sold = await h.SellAsync(await h.DefinitionAsync());

        var result = await h.Benefits().TryReserveBenefitsAsync(h.Reserve(sold, Guid.NewGuid(), service: h.OtherService));

        Assert.Equal(RetentionErrorCodes.BenefitServiceMismatch, result.Error!.Code);
    }

    [Theory]
    [InlineData(RetentionScopeCodes.PurchaseLocationOnly)]
    [InlineData(RetentionScopeCodes.SelectedLocations)]
    public async Task Reserve_OutsideRedemptionScope_IsDenied(string scope)
    {
        var h = await NewAsync();
        var definition = await h.DefinitionAsync(
            redemptionScope: scope,
            redemptionLocations: scope == RetentionScopeCodes.SelectedLocations ? [h.LocationA] : null);
        var sold = await h.SellAsync(definition, location: h.LocationA);

        var result = await h.Benefits().TryReserveBenefitsAsync(h.Reserve(sold, Guid.NewGuid(), location: h.LocationB));

        Assert.Equal(RetentionErrorCodes.ScopeLocationDenied, result.Error!.Code);
    }

    [Fact]
    public async Task Reserve_AllLocationsScope_AllowsAnyBranch()
    {
        var h = await NewAsync();
        var sold = await h.SellAsync(await h.DefinitionAsync(), location: h.LocationA);

        var result = await h.Benefits().TryReserveBenefitsAsync(h.Reserve(sold, Guid.NewGuid(), location: h.LocationB));

        Assert.True(result.IsSuccess, result.Error?.Code);
    }

    [Fact]
    public async Task Reserve_OtherTenantPlatformCustomer_IsNotFound()
    {
        var h = await NewAsync();
        var sold = await h.SellAsync(await h.DefinitionAsync());
        var request = h.Reserve(sold, Guid.NewGuid()) with { PlatformCustomerId = Guid.NewGuid() };

        var result = await h.Benefits().TryReserveBenefitsAsync(request);

        Assert.Equal(RetentionErrorCodes.PackageNotFound, result.Error!.Code);
    }

    [Fact]
    public async Task Reserve_ExpiredPackage_IsRejected()
    {
        var h = await NewAsync();
        var sold = await h.SellAsync(await h.DefinitionAsync());
        h.Clock.Now = h.Clock.Now.AddMonths(13);

        var result = await h.Benefits().TryReserveBenefitsAsync(h.Reserve(sold, Guid.NewGuid()));

        Assert.Equal(RetentionErrorCodes.PackageExpired, result.Error!.Code);
    }

    [Fact]
    public async Task GetCoveredServices_ReportsReservedQuantity_UntilReleased()
    {
        var h = await NewAsync();
        var sold = await h.SellAsync(await h.DefinitionAsync());
        var booking = Guid.NewGuid();
        await h.Benefits().TryReserveBenefitsAsync(h.Reserve(sold, booking));
        await h.Db.SaveChangesAsync();

        var covered = await h.Benefits().GetCoveredServicesAsync(h.Tenant, booking);
        Assert.Equal(new CoveredBenefitService(h.Service, 1), Assert.Single(covered));

        await h.Benefits().ReleaseReservationsAsync(h.Tenant, booking, "customer", "cancelled");
        await h.Db.SaveChangesAsync();
        Assert.Empty(await h.Benefits().GetCoveredServicesAsync(h.Tenant, booking));
    }

    [Fact]
    public async Task ListRedeemable_FiltersByLocationScope()
    {
        var h = await NewAsync();
        var definition = await h.DefinitionAsync(redemptionScope: RetentionScopeCodes.PurchaseLocationOnly);
        await h.SellAsync(definition, location: h.LocationA);

        var atA = await h.Benefits().ListRedeemableAsync(h.Tenant, h.TenantCustomer, h.LocationA, null);
        var atB = await h.Benefits().ListRedeemableAsync(h.Tenant, h.TenantCustomer, h.LocationB, null);

        Assert.Single(atA.Value!.Items);
        Assert.Empty(atB.Value!.Items);
    }

    [Fact]
    public async Task ListRedeemable_OtherTenant_IsEmpty()
    {
        var h = await NewAsync();
        await h.SellAsync(await h.DefinitionAsync());

        var result = await h.Benefits().ListRedeemableAsync(Guid.NewGuid(), h.TenantCustomer, null, null);

        Assert.Empty(result.Value!.Items);
    }

    // ---- Void ----

    [Fact]
    public async Task Void_NonOwner_IsDenied()
    {
        var h = await NewAsync();
        var sold = await h.SellAsync(await h.DefinitionAsync());

        var result = await h.Packages().VoidAsync(h.Tenant, Manager, sold.Id, new VoidCustomerPackageRequest("oops"), "v1");

        Assert.Equal(RetentionErrorCodes.OwnerRequired, result.Error!.Code);
    }

    [Fact]
    public async Task Void_Owner_ReleasesReservationsAndForfeitsRemaining()
    {
        var h = await NewAsync();
        var sold = await h.SellAsync(await h.DefinitionAsync(quantity: 3));
        await h.Benefits().TryReserveBenefitsAsync(h.Reserve(sold, Guid.NewGuid()));
        await h.Db.SaveChangesAsync();

        var voided = await h.Packages().VoidAsync(h.Tenant, Owner, sold.Id, new VoidCustomerPackageRequest("refund"), "v1");

        Assert.True(voided.IsSuccess, voided.Error?.Code);
        Assert.Equal("voided", voided.Value!.Status);
        var balance = await h.BalanceAsync(sold);
        Assert.Equal(0, balance.Reserved);
        Assert.Equal(3, balance.Forfeited);
        Assert.Equal(0, balance.Remaining);
        Assert.Contains(await h.Db.Set<BenefitTransaction>().ToListAsync(), x => x.Kind == BenefitTransactionKind.Void);
    }

    [Fact]
    public async Task Void_SameKey_Replays()
    {
        var h = await NewAsync();
        var sold = await h.SellAsync(await h.DefinitionAsync());
        var first = await h.Packages().VoidAsync(h.Tenant, Owner, sold.Id, new VoidCustomerPackageRequest("r"), "v1");
        var second = await h.Packages().VoidAsync(h.Tenant, Owner, sold.Id, new VoidCustomerPackageRequest("r"), "v1");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess, second.Error?.Code);
        Assert.Equal(1, await h.Db.Set<BenefitTransaction>().CountAsync(x => x.Kind == BenefitTransactionKind.Void));
    }

    // ---- Expiry ----

    [Fact]
    public async Task Expiry_DuePackage_ForfeitsRemainingAndReleasesReservations()
    {
        var h = await NewAsync();
        var sold = await h.SellAsync(await h.DefinitionAsync(quantity: 3));
        await h.Benefits().TryReserveBenefitsAsync(h.Reserve(sold, Guid.NewGuid()));
        await h.Db.SaveChangesAsync();
        h.Clock.Now = h.Clock.Now.AddMonths(12).AddDays(1);

        var expired = await h.Expiry().ExpireDuePackagesAsync(50);

        Assert.Equal(1, expired);
        var package = await h.Db.Set<CustomerPackage>().AsNoTracking().SingleAsync();
        Assert.Equal(CustomerPackageStatus.Expired, package.Status);
        var balance = await h.BalanceAsync(sold);
        Assert.Equal(0, balance.Reserved);
        Assert.Equal(3, balance.Forfeited);
    }

    [Fact]
    public async Task Expiry_NotDuePackage_IsUntouched()
    {
        var h = await NewAsync();
        await h.SellAsync(await h.DefinitionAsync());

        Assert.Equal(0, await h.Expiry().ExpireDuePackagesAsync(50));
        Assert.Equal(CustomerPackageStatus.Active, (await h.Db.Set<CustomerPackage>().AsNoTracking().SingleAsync()).Status);
    }

    // ---- F1 summary ----

    [Fact]
    public async Task Summary_CountsOnlyActivePackages()
    {
        var h = await NewAsync();
        var definition = await h.DefinitionAsync(quantity: 2);
        var first = await h.SellAsync(definition, "a");
        await h.SellAsync(definition, "b");
        await h.Packages().VoidAsync(h.Tenant, Owner, first.Id, new VoidCustomerPackageRequest(), "v");

        var summary = await new CustomerPackageSummaryQuery(h.Db, h.Clock).GetSummaryAsync(h.Tenant, h.TenantCustomer);

        Assert.Equal(1, summary!.ActiveCount);
        Assert.Equal(2, summary.TotalRemainingCredits);
    }
}
