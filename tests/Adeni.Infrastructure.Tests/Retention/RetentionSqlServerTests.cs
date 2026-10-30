namespace Adeni.Infrastructure.Tests.Retention;

using Adeni.Application.Retention;
using Adeni.Domain.Booking;
using Adeni.Domain.Identity;
using Adeni.Domain.Retention;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Real SQL Server (rowversion) concurrency proofs for the benefit ledger. Skipped unless
/// <c>ADENI_SQLSERVER_TESTS=1</c> and <c>ConnectionStrings__AdeniDb</c> points at an isolated, DACPAC-published database.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class RetentionSqlServerTests
{
    private sealed class SqlFactAttribute : FactAttribute
    {
        public SqlFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("ADENI_SQLSERVER_TESTS") != "1")
            {
                Skip = "Set ADENI_SQLSERVER_TESTS=1 and publish DACPAC to isolated SQL Server.";
            }
        }
    }

    private sealed class Guard : IRetentionEntitlementGuard
    {
        public Task<bool> IsLoyaltyBasicEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Domain.Common.Result> EnsureLoyaltyBasicEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());

        public Task<bool> IsMembershipsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Domain.Common.Result> EnsureMembershipsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());

        public Task<bool> IsRecurringBillingEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Domain.Common.Result> EnsureRecurringBillingEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());

        public Task<bool> IsPackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Domain.Common.Result> EnsurePackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());

        public Task<bool> IsLoyaltyTiersEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Domain.Common.Result> EnsureLoyaltyTiersEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());

        public Task<bool> IsReferralsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Domain.Common.Result> EnsureReferralsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());

        public Task<bool> IsCrossLocationRetentionEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Domain.Common.Result> EnsureCrossLocationRetentionEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());

        public Task<bool> IsAdvancedRetentionAnalyticsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Domain.Common.Result> EnsureAdvancedRetentionAnalyticsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());
    }

    private sealed class Lookup(AdeniDbContext db) : IRetentionServiceLookup
    {
        public async Task<IReadOnlyList<ServiceOfferingLookup>> FindAsync(
            Guid tenantId, IReadOnlyCollection<Guid> serviceOfferingIds, CancellationToken cancellationToken = default) =>
            await db.ServiceOfferings.Where(x => x.TenantId == tenantId && serviceOfferingIds.Contains(x.Id))
                .Select(x => new ServiceOfferingLookup(x.Id, x.Name, true)).ToListAsync(cancellationToken);
    }

    /// <summary>Holds every participant at SaveChanges until all have staged their writes, forcing a true race.</summary>
    private sealed class Gate(int participants) : SaveChangesInterceptor
    {
        private int arrived;
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref arrived) == participants)
            {
                release.TrySetResult();
            }

            await release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return result;
        }
    }

    private sealed record Seeded(Guid Tenant, Guid Location, Guid PlatformCustomer, Guid TenantCustomer, Guid Service);

    private sealed record ReferralCapSeed(
        Guid Tenant,
        Guid ReferrerTenantCustomer,
        Guid ServiceId,
        IReadOnlyList<Guid> PendingBookingIds);

    private static readonly RetentionActor Owner = new("owner-1", "owner");

    private static AdeniDbContext Db(IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<AdeniDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__AdeniDb"));
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return new AdeniDbContext(builder.Options, new TenantContext());
    }

    private static async Task<Seeded> SeedAsync()
    {
        await using var db = Db();
        var now = DateTimeOffset.UtcNow;
        var s = new Seeded(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        db.Add(new Tenant { Id = s.Tenant, Name = "Retention SQL test", CreatedAt = now });
        db.Add(new BusinessProfile { TenantId = s.Tenant, CategorySlug = "salon", Phone = "+10000000000" });
        db.Add(new Customer { Id = s.PlatformCustomer, Name = "Ada", Auth0Sub = $"retention-{s.PlatformCustomer:N}", CreatedAt = now });
        db.Add(new BusinessLocation { Id = s.Location, TenantId = s.Tenant, Name = "Main", Slug = "main", IsPrimary = true, CreatedAt = now });
        db.Add(new TenantCustomer
        {
            Id = s.TenantCustomer, TenantId = s.Tenant, CustomerId = s.PlatformCustomer, DisplayName = "Ada",
            CreatedAt = now, UpdatedAt = now, LastSeenAt = now,
        });
        db.Add(new Domain.Booking.ServiceOffering { Id = s.Service, TenantId = s.Tenant, Name = "Haircut", PriceAmount = 5000 });
        await db.SaveChangesAsync();
        return s;
    }

    private static async Task<ReferralCapSeed> SeedReferralCapAsync(int rewardedCount, int cap)
    {
        await using var db = Db();
        var now = DateTimeOffset.UtcNow;
        var tenant = Guid.NewGuid();
        var referrerRoster = Guid.NewGuid();
        var referrerPlatform = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var codeId = Guid.NewGuid();
        db.Add(new Tenant { Id = tenant, Name = "Referral cap SQL", CreatedAt = now });
        db.Add(new BusinessProfile { TenantId = tenant, CategorySlug = "salon", Phone = "+10000000001" });
        db.Add(new Customer { Id = referrerPlatform, Name = "Referrer", Auth0Sub = $"ref-{referrerPlatform:N}", CreatedAt = now });
        db.Add(new TenantCustomer
        {
            Id = referrerRoster, TenantId = tenant, CustomerId = referrerPlatform, DisplayName = "Referrer",
            CreatedAt = now, UpdatedAt = now, LastSeenAt = now,
        });
        db.Add(new LoyaltyProgram
        {
            Id = Guid.NewGuid(), TenantId = tenant, IsEnabled = true, Currency = "NGN", EarnPoints = 1, EarnPerAmount = 100,
            RedeemPoints = 100, RedeemValue = 500, MinRedeemPoints = 0, CreatedAt = now, UpdatedAt = now, UpdatedBy = "owner",
        });
        db.Add(new ReferralProgram
        {
            Id = Guid.NewGuid(), TenantId = tenant, IsEnabled = true, ReferrerRewardPoints = 10, RefereeRewardPoints = 5,
            MaxRewardsPerReferrer = cap, CreatedAt = now, UpdatedAt = now, UpdatedBy = "owner",
        });
        db.Add(new ReferralCode
        {
            Id = codeId, TenantId = tenant, TenantCustomerId = referrerRoster, Code = "CAPTEST01", CreatedAt = now,
        });
        db.Add(new ServiceOffering
        {
            Id = serviceId, TenantId = tenant, Name = "Cut", PriceAmount = 100m, Currency = "NGN", DurationMinutes = 30,
            CreatedAt = now, UpdatedAt = now,
        });

        for (var i = 0; i < rewardedCount; i++)
        {
            var refereeRoster = Guid.NewGuid();
            var refereePlatform = Guid.NewGuid();
            db.Add(new Customer { Id = refereePlatform, Name = $"Ref{i}", Auth0Sub = $"ref-{refereePlatform:N}", CreatedAt = now });
            db.Add(new TenantCustomer
            {
                Id = refereeRoster, TenantId = tenant, CustomerId = refereePlatform, DisplayName = $"Ref{i}",
                CreatedAt = now, UpdatedAt = now, LastSeenAt = now,
            });
            db.Add(new ReferralAttribution
            {
                Id = Guid.NewGuid(), TenantId = tenant, ReferrerTenantCustomerId = referrerRoster, RefereeTenantCustomerId = refereeRoster,
                ReferralCodeId = codeId, Status = ReferralAttributionStatus.Rewarded, RewardedAt = now, CreatedAt = now,
            });
        }

        var pendingBookings = new List<Guid>();
        for (var j = 0; j < 2; j++)
        {
            var refereeRoster = Guid.NewGuid();
            var refereePlatform = Guid.NewGuid();
            var bookingId = Guid.NewGuid();
            pendingBookings.Add(bookingId);
            db.Add(new Customer { Id = refereePlatform, Name = $"Pending{j}", Auth0Sub = $"pend-{refereePlatform:N}", CreatedAt = now });
            db.Add(new TenantCustomer
            {
                Id = refereeRoster, TenantId = tenant, CustomerId = refereePlatform, DisplayName = $"Pending{j}",
                CreatedAt = now, UpdatedAt = now, LastSeenAt = now,
            });
            db.Add(new ReferralAttribution
            {
                Id = Guid.NewGuid(), TenantId = tenant, ReferrerTenantCustomerId = referrerRoster, RefereeTenantCustomerId = refereeRoster,
                ReferralCodeId = codeId, Status = ReferralAttributionStatus.Pending, CreatedAt = now,
            });
            db.Add(new BookingRecord
            {
                Id = bookingId, TenantId = tenant, ServiceOfferingId = serviceId, CustomerId = refereePlatform,
                StartAt = now, EndAt = now.AddHours(1), Status = BookingStatus.Paid, PaidAt = now, CreatedAt = now, UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync();
        return new ReferralCapSeed(tenant, referrerRoster, serviceId, pendingBookings);
    }

    private static async Task CleanupReferralCapAsync(ReferralCapSeed s)
    {
        await using var db = Db();
        var customerIds = await db.Set<TenantCustomer>().AsNoTracking()
            .Where(x => x.TenantId == s.Tenant)
            .Select(x => x.CustomerId)
            .ToListAsync();
        await db.Set<LoyaltyTransaction>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<LoyaltyAccount>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<ReferralAttribution>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<ReferralCode>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<ReferralProgram>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<LoyaltyProgram>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Bookings.Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<TenantCustomer>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.ServiceOfferings.Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<BusinessLocation>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<BusinessProfile>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Tenants.Where(x => x.Id == s.Tenant).ExecuteDeleteAsync();
        if (customerIds.Count > 0)
        {
            await db.Customers.Where(x => customerIds.Contains(x.Id)).ExecuteDeleteAsync();
        }
    }

    private static async Task CleanupAsync(Seeded s)
    {
        await using var db = Db();
        await db.Set<BenefitTransaction>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<BenefitOperation>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<BenefitBalance>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<CustomerPackage>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<PackageBenefit>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<PackageDefinition>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<TenantCustomer>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.ServiceOfferings.Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<BusinessLocation>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Set<BusinessProfile>().Where(x => x.TenantId == s.Tenant).ExecuteDeleteAsync();
        await db.Tenants.Where(x => x.Id == s.Tenant).ExecuteDeleteAsync();
        await db.Customers.Where(x => x.Id == s.PlatformCustomer).ExecuteDeleteAsync();
    }

    private static async Task<PackageDefinitionResponse> DefinitionAsync(AdeniDbContext db, Seeded s, int quantity)
    {
        var created = await new PackageCatalogService(db, new Guard(), new Lookup(db), TimeProvider.System).CreateAsync(
            s.Tenant, Owner,
            new CreatePackageDefinitionRequest("Cut pack", 12000m, "NGN", [new PackageBenefitInput(s.Service, quantity)], null, null, null, null, null, null));
        Assert.True(created.IsSuccess, created.Error?.Code);
        return created.Value!;
    }

    private static async Task<CustomerPackageResponse> SellAsync(AdeniDbContext db, Seeded s, PackageDefinitionResponse definition)
    {
        var sold = await new CustomerPackageService(db, new Guard(), new DomainEventCollector(), TimeProvider.System).SellAsync(
            s.Tenant, new RetentionActor("manager-1", "manager"), s.TenantCustomer,
            new SellPackageRequest(definition.Id, s.Location, SellPaymentMethods.Till), Guid.NewGuid().ToString());
        Assert.True(sold.IsSuccess, sold.Error?.Code);
        return sold.Value!;
    }

    [SqlFact]
    public async Task ConcurrentReserve_OfTheLastCredit_ExactlyOneBookingWins()
    {
        var s = await SeedAsync();
        try
        {
            await using var seed = Db();
            var package = await SellAsync(seed, s, await DefinitionAsync(seed, s, quantity: 1));
            var balanceId = package.Balances[0].Id;

            async Task<bool> TryBookAsync(AdeniDbContext db)
            {
                var service = new BenefitRedemptionService(db, new DomainEventCollector(), TimeProvider.System, new Guard());
                var reserved = await service.TryReserveBenefitsAsync(new ReserveBenefitsRequest(
                    s.Tenant, s.PlatformCustomer, Guid.NewGuid(), s.Location, [s.Service], 1,
                    [new AppliedBenefitRequest(package.Id, balanceId, 1)], "customer", DateTimeOffset.UtcNow));
                Assert.True(reserved.IsSuccess, reserved.Error?.Code); // both validate against Remaining = 1
                try
                {
                    await db.SaveChangesAsync();
                    return true;
                }
                catch (DbUpdateException ex) when (service.IsBenefitConflict(ex))
                {
                    return false;
                }
            }

            var gate = new Gate(2);
            await using var a = Db(gate);
            await using var b = Db(gate);
            var outcomes = await Task.WhenAll(TryBookAsync(a), TryBookAsync(b));

            Assert.Single(outcomes, x => x);
            Assert.Single(outcomes, x => !x);

            await using var check = Db();
            var balance = await check.Set<BenefitBalance>().AsNoTracking().SingleAsync(x => x.Id == balanceId);
            Assert.Equal(1, balance.Reserved);
            Assert.Equal(0, balance.Remaining);
            Assert.Equal(1, await check.Set<BenefitTransaction>().CountAsync(x => x.TenantId == s.Tenant && x.Kind == BenefitTransactionKind.Reserve));
        }
        finally
        {
            await CleanupAsync(s);
        }
    }

    [SqlFact]
    public async Task ConcurrentReserve_WithEnoughCredits_NeverOverReserves()
    {
        var s = await SeedAsync();
        try
        {
            await using var seed = Db();
            var package = await SellAsync(seed, s, await DefinitionAsync(seed, s, quantity: 3));
            var balanceId = package.Balances[0].Id;

            async Task<bool> TryBookAsync(AdeniDbContext db)
            {
                var service = new BenefitRedemptionService(db, new DomainEventCollector(), TimeProvider.System, new Guard());
                var reserved = await service.TryReserveBenefitsAsync(new ReserveBenefitsRequest(
                    s.Tenant, s.PlatformCustomer, Guid.NewGuid(), s.Location, [s.Service], 1,
                    [new AppliedBenefitRequest(package.Id, balanceId, 1)], "customer", DateTimeOffset.UtcNow));
                Assert.True(reserved.IsSuccess);
                try
                {
                    await db.SaveChangesAsync();
                    return true;
                }
                catch (DbUpdateException ex) when (service.IsBenefitConflict(ex))
                {
                    return false; // The loser re-reads and retries in the booking path; counters must never exceed grants.
                }
            }

            var gate = new Gate(2);
            await using var a = Db(gate);
            await using var b = Db(gate);
            await Task.WhenAll(TryBookAsync(a), TryBookAsync(b));

            await using var check = Db();
            var balance = await check.Set<BenefitBalance>().AsNoTracking().SingleAsync(x => x.Id == balanceId);
            var sum = await check.Set<BenefitTransaction>().Where(x => x.BenefitBalanceId == balanceId).SumAsync(x => x.Delta);
            Assert.InRange(balance.Reserved, 1, 2);
            Assert.True(balance.Remaining >= 1);
            Assert.Equal(balance.Remaining, sum); // ledger always reconciles to the counters
        }
        finally
        {
            await CleanupAsync(s);
        }
    }

    [SqlFact]
    public async Task ConcurrentDuplicateActivation_GrantsExactlyOnce()
    {
        var s = await SeedAsync();
        try
        {
            Guid packageId;
            var intentId = Guid.NewGuid();
            await using (var seed = Db())
            {
                var definition = await DefinitionAsync(seed, s, quantity: 3);
                var pending = new CustomerPackage
                {
                    Id = Guid.NewGuid(), TenantId = s.Tenant, TenantCustomerId = s.TenantCustomer, PackageDefinitionId = definition.Id,
                    SoldChannel = PackageSoldChannel.Online, PaymentIntentId = intentId, Status = CustomerPackageStatus.PendingPayment,
                    NameSnapshot = definition.Name, ListPriceSnapshot = definition.ListPrice, CurrencySnapshot = definition.Currency,
                    RedemptionScopeSnapshot = PackageRedemptionScope.AllLocations, SoldBy = "customer", CreatedAt = DateTimeOffset.UtcNow,
                };
                seed.Add(pending);
                await seed.SaveChangesAsync();
                packageId = pending.Id;
            }

            var gate = new Gate(2);
            await using var a = Db(gate);
            await using var b = Db(gate);
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<PackagePurchaseActivation>.Instance;
            var results = await Task.WhenAll(
                new PackagePurchaseActivation(a, new DomainEventCollector(), TimeProvider.System, logger)
                    .ActivateForPaymentAsync(s.Tenant, intentId, 12000m, "NGN"),
                new PackagePurchaseActivation(b, new DomainEventCollector(), TimeProvider.System, logger)
                    .ActivateForPaymentAsync(s.Tenant, intentId, 12000m, "NGN"));

            Assert.All(results, Assert.True); // winner grants; loser observes Active and treats it as done

            await using var check = Db();
            Assert.Equal(CustomerPackageStatus.Active, (await check.Set<CustomerPackage>().AsNoTracking().SingleAsync(x => x.Id == packageId)).Status);
            Assert.Single(await check.Set<BenefitBalance>().Where(x => x.CustomerPackageId == packageId).ToArrayAsync());
            Assert.Single(await check.Set<BenefitTransaction>().Where(x => x.TenantId == s.Tenant && x.Kind == BenefitTransactionKind.Grant).ToArrayAsync());
        }
        finally
        {
            await CleanupAsync(s);
        }
    }

    [SqlFact]
    public async Task ConcurrentReferralCap_AtLimit_ExactlyOneExtraReward()
    {
        var s = await SeedReferralCapAsync(rewardedCount: 9, cap: 10);
        try
        {
            async Task QualifyAsync(AdeniDbContext db, Guid bookingId)
            {
                var service = new ReferralQualificationService(db, new Guard(), new DomainEventCollector(), TimeProvider.System);
                var result = await service.TryQualifyAndIssueRewardsForPaidBookingAsync(s.Tenant, bookingId, "sql-test");
                Assert.True(result.IsSuccess, result.Error?.Code);
                // Match payment/booking handlers: persist qualify-path Rejected/Qualified before another worker reads counts.
                await db.SaveChangesAsync();
            }

            // No SaveChanges gate: UPDLOCK serializes issue paths; a gate deadlocks when one worker waits on the lock before SaveChanges.
            await using var a = Db();
            await using var b = Db();
            await Task.WhenAll(QualifyAsync(a, s.PendingBookingIds[0]), QualifyAsync(b, s.PendingBookingIds[1]));

            await using var check = Db();
            var attributions = await check.Set<ReferralAttribution>().AsNoTracking()
                .Where(x => x.TenantId == s.Tenant && x.ReferrerTenantCustomerId == s.ReferrerTenantCustomer)
                .ToListAsync();
            Assert.Equal(11, attributions.Count);
            Assert.Equal(10, attributions.Count(x => x.Status == ReferralAttributionStatus.Rewarded));
            Assert.Equal(1, attributions.Count(x => x.Status == ReferralAttributionStatus.Rejected && x.RejectReason == "referrer_cap"));
        }
        finally
        {
            await CleanupReferralCapAsync(s);
        }
    }
}
