namespace Adeni.Infrastructure.Tests.Retention;

using Adeni.Application.Abstractions;
using Adeni.Application.Events;
using Adeni.Application.Retention;
using Adeni.Domain.Auditing;
using Adeni.Domain.Booking;
using Adeni.Domain.Retention;
using Adeni.Domain.Retention.Events;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore;

public sealed class LoyaltyServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly RetentionActor Owner = new("owner-1", "owner");
    private static readonly RetentionActor Manager = new("manager-1", "manager");
    private static readonly RetentionActor Receptionist = new("recep-1", "receptionist");

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Start;
    }

    private sealed class Guard(bool entitled) : IRetentionEntitlementGuard
    {
        public Task<bool> IsLoyaltyBasicEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(entitled);

        public Task<Domain.Common.Result> EnsureLoyaltyBasicEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(entitled
                ? Domain.Common.Result.Success()
                : Domain.Common.Result.Failure(RetentionErrorCodes.LoyaltyBasicNotEntitledError()));

        public Task<bool> IsMembershipsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Domain.Common.Result> EnsureMembershipsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());

        public Task<bool> IsRecurringBillingEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Domain.Common.Result> EnsureRecurringBillingEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());

        public Task<bool> IsPackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

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

        public CapturingAudit Audit { get; } = new();
        public Guid Tenant { get; } = Guid.NewGuid();
        public Guid OtherTenant { get; } = Guid.NewGuid();
        public Guid Customer { get; } = Guid.NewGuid();
        public Guid OtherTenantCustomer { get; } = Guid.NewGuid();

        public LoyaltyProgramService Programs(bool entitled = true) => new(Db, new Guard(entitled), new FixedClock(), Audit);

        public LoyaltyAccountService Accounts(bool entitled = true) => new(Db, new Guard(entitled), new FixedClock(), Audit);

        public DomainEventCollector Events { get; } = new();

        public LoyaltyRedemptionService Redemption(bool entitled = true) =>
            new(Db, new Guard(entitled), new FixedClock());

        public LoyaltyEarnService Earn(bool entitled = true) =>
            new(
                Db,
                new Guard(entitled),
                new BenefitRedemptionService(Db, Events, new FixedClock()),
                Redemption(entitled),
                Events,
                new FixedClock());

        public async Task SeedAsync()
        {
            Db.Add(new Tenant { Id = Tenant, Name = "Salon", CreatedAt = Start });
            Db.Add(new Tenant { Id = OtherTenant, Name = "Other", CreatedAt = Start });
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
            await Db.SaveChangesAsync();
        }

        public static UpsertLoyaltyProgramRequest Program(
            bool enabled = true,
            string currency = "NGN",
            int earnPoints = 1,
            decimal earnPerAmount = 100m,
            int redeemPoints = 100,
            decimal redeemValue = 500m,
            int minRedeem = 100,
            int? expiry = 12) =>
            new(enabled, currency, earnPoints, earnPerAmount, redeemPoints, redeemValue, minRedeem, expiry);

        public async Task SaveProgramAsync(Guid? tenant = null)
        {
            var saved = await Programs().UpsertAsync(tenant ?? Tenant, Owner, Program());
            Assert.True(saved.IsSuccess, saved.IsFailure ? saved.Error.Code : null);
        }
    }

    private static async Task<Harness> NewAsync()
    {
        var h = new Harness();
        await h.SeedAsync();
        return h;
    }

    // ---- Program ----

    [Fact]
    public async Task GetProgram_WhenNeverSaved_ReturnsSuggestedDefaultsNotConfigured()
    {
        var h = await NewAsync();

        var result = await h.Programs().GetAsync(h.Tenant);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Configured);
        Assert.False(result.Value.IsEnabled);
        Assert.Null(result.Value.Id);
        Assert.Equal(1, result.Value.EarnPoints);
        Assert.Equal(0, await h.Db.Set<LoyaltyProgram>().CountAsync());
    }

    [Fact]
    public async Task UpsertProgram_CreatesThenReplaces_AsSingleRowPerTenant()
    {
        var h = await NewAsync();

        var created = await h.Programs().UpsertAsync(h.Tenant, Owner, Harness.Program(enabled: false));
        var replaced = await h.Programs().UpsertAsync(
            h.Tenant, Owner, Harness.Program(enabled: true, currency: "cad", earnPoints: 2, redeemValue: 800m, expiry: null));

        Assert.True(created.IsSuccess);
        Assert.True(replaced.IsSuccess);
        Assert.Equal(created.Value!.Id, replaced.Value!.Id);
        Assert.True(replaced.Value.Configured);
        Assert.True(replaced.Value.IsEnabled);
        Assert.Equal("CAD", replaced.Value.Currency);
        Assert.Equal(2, replaced.Value.EarnPoints);
        Assert.Null(replaced.Value.PointsExpiryMonths);
        Assert.Equal(1, await h.Db.Set<LoyaltyProgram>().CountAsync());
        Assert.Equal(2, h.Audit.Entries.Count(e => e.Action == AuditActions.RetentionLoyaltyProgramSaved));

        var read = await h.Programs().GetAsync(h.Tenant);
        Assert.Equal(800m, read.Value!.RedeemValue);
    }

    [Theory]
    [InlineData("XXX", 1, 100, 100, 500, 100, 12)]
    [InlineData("NGN", 0, 100, 100, 500, 100, 12)]
    [InlineData("NGN", 1001, 100, 100, 500, 100, 12)]
    [InlineData("NGN", 1, 0, 100, 500, 100, 12)]
    [InlineData("NGN", 1, 100.123, 100, 500, 100, 12)]
    [InlineData("NGN", 1, 100, 0, 500, 100, 12)]
    [InlineData("NGN", 1, 100, 100, 0, 100, 12)]
    [InlineData("NGN", 1, 100, 100, 500, -1, 12)]
    [InlineData("NGN", 1, 100, 100, 500, 100, 0)]
    [InlineData("NGN", 1, 100, 100, 500, 100, 61)]
    // Loss guard: 100 points worth 150 when 100 points only cost 100 of spend (> 100% back).
    [InlineData("NGN", 1, 1, 100, 150, 100, 12)]
    public async Task UpsertProgram_InvalidInput_ReturnsValidation(
        string currency, int earnPoints, double earnPerAmount, int redeemPoints, double redeemValue, int minRedeem, int expiry)
    {
        var h = await NewAsync();

        var result = await h.Programs().UpsertAsync(
            h.Tenant,
            Owner,
            Harness.Program(
                currency: currency, earnPoints: earnPoints, earnPerAmount: (decimal)earnPerAmount, redeemPoints: redeemPoints,
                redeemValue: (decimal)redeemValue, minRedeem: minRedeem, expiry: expiry));

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.Validation, result.Error!.Code);
        Assert.Equal(0, await h.Db.Set<LoyaltyProgram>().CountAsync());
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("receptionist")]
    public async Task UpsertProgram_NonOwner_IsDenied(string role)
    {
        var h = await NewAsync();

        var result = await h.Programs().UpsertAsync(h.Tenant, new RetentionActor("u", role), Harness.Program());

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.OwnerRequired, result.Error!.Code);
    }

    [Fact]
    public async Task Program_IsIsolatedPerTenant()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();

        var other = await h.Programs().GetAsync(h.OtherTenant);

        Assert.False(other.Value!.Configured);
    }

    // ---- Entitlement ----

    [Fact]
    public async Task EveryMethod_WhenNotEntitled_ReturnsLoyaltyEntitlementError()
    {
        var h = await NewAsync();
        var programs = h.Programs(entitled: false);
        var accounts = h.Accounts(entitled: false);

        var errors = new[]
        {
            (await programs.GetAsync(h.Tenant)).Error,
            (await programs.UpsertAsync(h.Tenant, Owner, Harness.Program())).Error,
            (await accounts.GetForCustomerAsync(h.Tenant, h.Customer)).Error,
            (await accounts.ListTransactionsAsync(h.Tenant, h.Customer, 10)).Error,
            (await accounts.AdjustAsync(h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(5, "x"), "k")).Error,
        };

        Assert.All(errors, error => Assert.Equal(RetentionErrorCodes.LoyaltyBasicNotEntitled, error!.Code));
    }

    // ---- Account / ledger ----

    [Fact]
    public async Task GetAccount_WithoutAccount_ReturnsZerosWithoutCreatingRow()
    {
        var h = await NewAsync();

        var result = await h.Accounts().GetForCustomerAsync(h.Tenant, h.Customer);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value!.PointsBalance);
        Assert.Equal(h.Customer, result.Value.TenantCustomerId);
        Assert.Equal(0, await h.Db.Set<LoyaltyAccount>().CountAsync());
    }

    [Fact]
    public async Task Adjust_PositiveThenNegative_UpdatesBalanceAndAppendsOrderedLedger()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();

        var plus = await h.Accounts().AdjustAsync(h.Tenant, Manager, h.Customer, new AdjustLoyaltyPointsRequest(120, "  Welcome bonus "), "k1");
        var minus = await h.Accounts().AdjustAsync(h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(-20, "Correction"), "k2");

        Assert.True(plus.IsSuccess, plus.IsFailure ? plus.Error.Code : null);
        Assert.Equal(120, plus.Value!.Account.PointsBalance);
        Assert.Equal(1, plus.Value.Transaction.Sequence);
        Assert.Equal("adjust", plus.Value.Transaction.Kind);
        Assert.Equal("Welcome bonus", plus.Value.Transaction.Reason);
        Assert.Equal(120, plus.Value.Transaction.BalanceAfter);
        Assert.True(minus.IsSuccess, minus.IsFailure ? minus.Error.Code : null);
        Assert.Equal(100, minus.Value!.Account.PointsBalance);
        Assert.Equal(2, minus.Value.Transaction.Sequence);
        // Adjustments never count towards the tier metric.
        Assert.Equal(0, minus.Value.Account.LifetimeEarned);
        Assert.Equal(0, minus.Value.Account.LifetimeRedeemed);

        var ledger = await h.Accounts().ListTransactionsAsync(h.Tenant, h.Customer, 50);
        Assert.Equal([2, 1], ledger.Value!.Items.Select(x => x.Sequence).ToArray());
        Assert.Equal(1, await h.Db.Set<LoyaltyAccount>().CountAsync());

        var account = await h.Accounts().GetForCustomerAsync(h.Tenant, h.Customer);
        Assert.Equal(100, account.Value!.PointsBalance);
    }

    [Fact]
    public async Task Adjust_BelowZero_IsRejectedAndLeavesBalanceUntouched()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();
        await h.Accounts().AdjustAsync(h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(50, "seed"), "k1");

        var result = await h.Accounts().AdjustAsync(h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(-51, "too much"), "k2");

        Assert.True(result.IsFailure);
        Assert.Equal(RetentionErrorCodes.LoyaltyInsufficientPoints, result.Error!.Code);
        var account = await h.Accounts().GetForCustomerAsync(h.Tenant, h.Customer);
        Assert.Equal(50, account.Value!.PointsBalance);
        Assert.Equal(1, await h.Db.Set<LoyaltyTransaction>().CountAsync());
    }

    [Fact]
    public async Task Adjust_FirstAdjustmentNegative_DoesNotCreateAccount()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();

        var result = await h.Accounts().AdjustAsync(h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(-1, "nope"), "k1");

        Assert.Equal(RetentionErrorCodes.LoyaltyInsufficientPoints, result.Error!.Code);
        Assert.Equal(0, await h.Db.Set<LoyaltyAccount>().CountAsync());
        Assert.Equal(0, await h.Db.Set<LoyaltyTransaction>().CountAsync());
    }

    [Fact]
    public async Task Adjust_WithoutProgram_ReturnsNotConfigured()
    {
        var h = await NewAsync();

        var result = await h.Accounts().AdjustAsync(h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(10, "x"), "k");

        Assert.Equal(RetentionErrorCodes.LoyaltyProgramNotConfigured, result.Error!.Code);
    }

    [Fact]
    public async Task Adjust_DisabledProgram_StillAllowsStaffCorrection()
    {
        var h = await NewAsync();
        await h.Programs().UpsertAsync(h.Tenant, Owner, Harness.Program(enabled: false));

        var result = await h.Accounts().AdjustAsync(h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(10, "x"), "k");

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(0, "reason")]
    [InlineData(1_000_001, "reason")]
    [InlineData(-1_000_001, "reason")]
    [InlineData(5, "")]
    [InlineData(5, "   ")]
    public async Task Adjust_InvalidInput_ReturnsValidation(int points, string reason)
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();

        var result = await h.Accounts().AdjustAsync(h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(points, reason), "k");

        Assert.Equal(RetentionErrorCodes.Validation, result.Error!.Code);
    }

    [Fact]
    public async Task Adjust_ReasonOver500Characters_ReturnsValidation()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();

        var result = await h.Accounts().AdjustAsync(
            h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(5, new string('x', 501)), "k");

        Assert.Equal(RetentionErrorCodes.Validation, result.Error!.Code);
    }

    [Fact]
    public async Task Adjust_MissingIdempotencyKey_IsRejected()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();

        var result = await h.Accounts().AdjustAsync(h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(5, "x"), null);

        Assert.Equal(RetentionErrorCodes.IdempotencyKeyRequired, result.Error!.Code);
    }

    [Fact]
    public async Task Adjust_SameKeySameBody_ReplaysWithoutDoubleApplying()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();
        var request = new AdjustLoyaltyPointsRequest(40, "Goodwill");

        var first = await h.Accounts().AdjustAsync(h.Tenant, Manager, h.Customer, request, "same");
        var replay = await h.Accounts().AdjustAsync(h.Tenant, Manager, h.Customer, request, "same");

        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value!.Transaction.Id, replay.Value!.Transaction.Id);
        Assert.Equal(40, replay.Value.Account.PointsBalance);
        Assert.Equal(1, await h.Db.Set<LoyaltyTransaction>().CountAsync());
        Assert.Single(h.Audit.Entries, e => e.Action == AuditActions.RetentionLoyaltyPointsAdjusted);
    }

    [Fact]
    public async Task Adjust_SameKeyDifferentBody_ReturnsIdempotencyConflict()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();
        await h.Accounts().AdjustAsync(h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(40, "Goodwill"), "same");

        var conflict = await h.Accounts().AdjustAsync(h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(41, "Goodwill"), "same");

        Assert.Equal(RetentionErrorCodes.IdempotencyConflict, conflict.Error!.Code);
        Assert.Equal(1, await h.Db.Set<LoyaltyTransaction>().CountAsync());
    }

    [Fact]
    public async Task Adjust_Receptionist_IsDenied_ButCanRead()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();

        var denied = await h.Accounts().AdjustAsync(h.Tenant, Receptionist, h.Customer, new AdjustLoyaltyPointsRequest(5, "x"), "k");
        var read = await h.Accounts().GetForCustomerAsync(h.Tenant, h.Customer);

        Assert.Equal(RetentionErrorCodes.OwnerRequired, denied.Error!.Code);
        Assert.True(read.IsSuccess);
    }

    [Fact]
    public async Task Adjust_CrossTenantCustomer_ReturnsNotFound()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();

        var adjust = await h.Accounts().AdjustAsync(
            h.Tenant, Owner, h.OtherTenantCustomer, new AdjustLoyaltyPointsRequest(5, "x"), "k");
        var account = await h.Accounts().GetForCustomerAsync(h.Tenant, h.OtherTenantCustomer);
        var ledger = await h.Accounts().ListTransactionsAsync(h.Tenant, h.OtherTenantCustomer, 10);

        Assert.Equal("not_found", adjust.Error!.Code);
        Assert.Equal("not_found", account.Error!.Code);
        Assert.Equal("not_found", ledger.Error!.Code);
        Assert.Equal(0, await h.Db.Set<LoyaltyAccount>().CountAsync());
    }

    [Fact]
    public async Task Ledger_IsIsolatedPerTenant_AndIdempotencyKeysDoNotCollideAcrossTenants()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();
        await h.SaveProgramAsync(h.OtherTenant);
        await h.Accounts().AdjustAsync(h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(10, "a"), "shared-key");
        await h.Accounts().AdjustAsync(h.OtherTenant, Owner, h.OtherTenantCustomer, new AdjustLoyaltyPointsRequest(99, "b"), "shared-key");

        var mine = await h.Accounts().ListTransactionsAsync(h.Tenant, h.Customer, 50);
        var theirs = await h.Accounts().ListTransactionsAsync(h.OtherTenant, h.OtherTenantCustomer, 50);
        // Tenant A asking for tenant B's customer must not see tenant B's ledger.
        var crossRead = await h.Accounts().ListTransactionsAsync(h.Tenant, h.OtherTenantCustomer, 50);

        Assert.Equal([10], mine.Value!.Items.Select(x => x.Points).ToArray());
        Assert.Equal([99], theirs.Value!.Items.Select(x => x.Points).ToArray());
        Assert.True(crossRead.IsFailure);
    }

    [Fact]
    public async Task ListTransactions_ClampsLimit()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();
        for (var i = 0; i < 3; i++)
        {
            await h.Accounts().AdjustAsync(h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(1, $"n{i}"), $"k{i}");
        }

        var limited = await h.Accounts().ListTransactionsAsync(h.Tenant, h.Customer, 2);
        var defaulted = await h.Accounts().ListTransactionsAsync(h.Tenant, h.Customer, 0);

        Assert.Equal(2, limited.Value!.Items.Count);
        Assert.Equal(3, defaulted.Value!.Items.Count);
    }

    [Fact]
    public async Task Audit_RecordsIdsAndDelta_NeverTheReasonText()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();

        await h.Accounts().AdjustAsync(h.Tenant, Owner, h.Customer, new AdjustLoyaltyPointsRequest(7, "private staff note"), "k");

        var entry = Assert.Single(h.Audit.Entries, e => e.Action == AuditActions.RetentionLoyaltyPointsAdjusted);
        Assert.Equal("owner-1", entry.ActorId);
        Assert.Contains(h.Tenant.ToString(), entry.MetadataJson);
        Assert.Contains("\"points\":7", entry.MetadataJson);
        Assert.DoesNotContain("private staff note", entry.MetadataJson);
    }

    [Fact]
    public async Task StageEarn_ForPaidBooking_IsIdempotentAndFloorsPoints()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();
        var roster = h.Db.TenantCustomers.Single(x => x.Id == h.Customer);
        Assert.NotNull(roster.CustomerId);
        var customerId = roster.CustomerId!.Value;
        var serviceId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        h.Db.Add(new BusinessProfile { TenantId = h.Tenant, CategorySlug = "salon", Phone = "+10000000000" });
        h.Db.Add(new ServiceOffering
        {
            Id = serviceId,
            TenantId = h.Tenant,
            Name = "Cut",
            PriceAmount = 250m,
            Currency = "NGN",
            DurationMinutes = 30,
            CreatedAt = Start,
            UpdatedAt = Start,
        });
        h.Db.Add(new BookingRecord
        {
            Id = bookingId,
            TenantId = h.Tenant,
            ServiceOfferingId = serviceId,
            CustomerId = customerId,
            StartAt = Start,
            EndAt = Start.AddHours(1),
            Status = BookingStatus.Paid,
            PaidAt = Start,
            CreatedAt = Start,
            UpdatedAt = Start,
            Lines =
            [
                new BookingLine
                {
                    Id = Guid.NewGuid(),
                    BookingId = bookingId,
                    TenantId = h.Tenant,
                    ServiceOfferingId = serviceId,
                    SortOrder = 0,
                    PriceAmount = 250m,
                    DurationMinutes = 30,
                    ServiceName = "Cut",
                },
            ],
        });
        await h.Db.SaveChangesAsync();

        var earn = h.Earn();
        var first = await earn.StageEarnForPaidBookingAsync(h.Tenant, bookingId, "test");
        var second = await earn.StageEarnForPaidBookingAsync(h.Tenant, bookingId, "test");
        await h.Db.SaveChangesAsync();

        Assert.True(first.IsSuccess);
        Assert.Equal(2, first.Value);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, second.Value);
        Assert.Equal(1, await h.Db.Set<LoyaltyTransaction>().CountAsync(x => x.Points > 0));
        var account = await h.Db.Set<LoyaltyAccount>().SingleAsync(x => x.TenantCustomerId == h.Customer);
        Assert.Equal(2, account.PointsBalance);
        Assert.Equal(2, account.LifetimeEarned);
        Assert.IsType<LoyaltyPointsEarned>(Assert.Single(h.Events.TakeAll()));
    }

    [Fact]
    public async Task StageRedeem_ForBooking_DebitsPointsAndRecordsDiscount()
    {
        var h = await NewAsync();
        await h.SaveProgramAsync();
        var roster = h.Db.TenantCustomers.Single(x => x.Id == h.Customer);
        var account = LoyaltyAccount.Open(h.Tenant, h.Customer, Start);
        account.PointsBalance = 500;
        h.Db.Add(account);
        var bookingId = Guid.NewGuid();
        await h.Db.SaveChangesAsync();

        var redeem = h.Redemption();
        var staged = await redeem.StageRedeemForBookingAsync(
            new StageLoyaltyRedeemRequest(h.Tenant, roster.CustomerId!.Value, bookingId, 100, 5000m, "test", Start));
        await h.Db.SaveChangesAsync();

        Assert.True(staged.IsSuccess);
        Assert.Equal(100, staged.Value);
        var row = await h.Db.Set<LoyaltyTransaction>().SingleAsync(x => x.BookingId == bookingId);
        Assert.Equal(-100, row.Points);
        Assert.Equal(500m, row.DiscountAmount);
        Assert.Equal(400, (await h.Db.Set<LoyaltyAccount>().SingleAsync()).PointsBalance);
    }
}
