namespace Adeni.Infrastructure.Tests.Retention;

using Adeni.Application.Retention;
using Adeni.Domain.Booking;
using Adeni.Domain.Retention;
using Adeni.Domain.Retention.Events;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Context;
using Adeni.Infrastructure.Events;
using Adeni.Infrastructure.Persistence;
using Adeni.Infrastructure.Retention;
using Microsoft.EntityFrameworkCore;

public sealed class ReferralServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly RetentionActor Owner = new("owner-1", "owner");

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Start;
    }

    private sealed class Guard(bool entitled = true) : IRetentionEntitlementGuard
    {
        public Task<bool> IsLoyaltyBasicEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(entitled);

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

        public Task<bool> IsReferralsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(entitled);

        public Task<Domain.Common.Result> EnsureReferralsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(entitled
                ? Domain.Common.Result.Success()
                : Domain.Common.Result.Failure(RetentionErrorCodes.ReferralsNotEntitledError()));

        public Task<bool> IsCrossLocationRetentionEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Domain.Common.Result> EnsureCrossLocationRetentionEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());

        public Task<bool> IsAdvancedRetentionAnalyticsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<Domain.Common.Result> EnsureAdvancedRetentionAnalyticsEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Domain.Common.Result.Success());
    }

    [Fact]
    public async Task AttachCode_CreatesPendingAttribution_IgnoresSelfReferral()
    {
        var tenant = Guid.NewGuid();
        var referrerRoster = Guid.NewGuid();
        var refereeRoster = Guid.NewGuid();
        var refereePlatform = Guid.NewGuid();
        await using var db = NewDb();
        db.Add(new Tenant { Id = tenant, Name = "Salon", CreatedAt = Start });
        db.Add(new TenantCustomer
        {
            Id = referrerRoster, TenantId = tenant, CustomerId = Guid.NewGuid(), DisplayName = "Ada",
            CreatedAt = Start, UpdatedAt = Start, LastSeenAt = Start,
        });
        db.Add(new TenantCustomer
        {
            Id = refereeRoster, TenantId = tenant, CustomerId = refereePlatform, DisplayName = "Bo",
            CreatedAt = Start, UpdatedAt = Start, LastSeenAt = Start,
        });
        db.Add(new ReferralProgram
        {
            Id = Guid.NewGuid(), TenantId = tenant, IsEnabled = true, ReferrerRewardPoints = 50, RefereeRewardPoints = 25,
            CreatedAt = Start, UpdatedAt = Start, UpdatedBy = "owner",
        });
        db.Add(new ReferralCode
        {
            Id = Guid.NewGuid(), TenantId = tenant, TenantCustomerId = referrerRoster, Code = "FRIEND01", CreatedAt = Start,
        });
        await db.SaveChangesAsync();

        var qualify = new ReferralQualificationService(db, new Guard(), new DomainEventCollector(), new FixedClock());
        var bookingId = Guid.NewGuid();
        await qualify.TryAttachCodeForBookingAsync(tenant, refereePlatform, "friend01", bookingId);
        await qualify.TryAttachCodeForBookingAsync(tenant, refereePlatform, "FRIEND01", bookingId);
        await db.SaveChangesAsync();

        var row = await db.Set<ReferralAttribution>().SingleAsync();
        Assert.Equal(ReferralAttributionStatus.Pending, row.Status);
        Assert.Equal(referrerRoster, row.ReferrerTenantCustomerId);
        Assert.Equal(refereeRoster, row.RefereeTenantCustomerId);
    }

    [Fact]
    public async Task Qualify_FirstPaidBooking_IssuesPointsOnce()
    {
        var tenant = Guid.NewGuid();
        var referrerRoster = Guid.NewGuid();
        var refereeRoster = Guid.NewGuid();
        var refereePlatform = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        await using var db = NewDb();
        db.Add(new Tenant { Id = tenant, Name = "Salon", CreatedAt = Start });
        db.Add(new BusinessProfile { TenantId = tenant, CategorySlug = "salon", Phone = "+10000000000" });
        db.Add(new TenantCustomer
        {
            Id = referrerRoster, TenantId = tenant, CustomerId = Guid.NewGuid(), DisplayName = "Ada",
            CreatedAt = Start, UpdatedAt = Start, LastSeenAt = Start,
        });
        db.Add(new TenantCustomer
        {
            Id = refereeRoster, TenantId = tenant, CustomerId = refereePlatform, DisplayName = "Bo",
            CreatedAt = Start, UpdatedAt = Start, LastSeenAt = Start,
        });
        db.Add(new LoyaltyProgram
        {
            Id = Guid.NewGuid(), TenantId = tenant, IsEnabled = true, Currency = "NGN", EarnPoints = 1, EarnPerAmount = 100,
            RedeemPoints = 100, RedeemValue = 500, MinRedeemPoints = 0, CreatedAt = Start, UpdatedAt = Start, UpdatedBy = "x",
        });
        db.Add(new ReferralProgram
        {
            Id = Guid.NewGuid(), TenantId = tenant, IsEnabled = true, ReferrerRewardPoints = 40, RefereeRewardPoints = 20,
            CreatedAt = Start, UpdatedAt = Start, UpdatedBy = "owner",
        });
        var codeId = Guid.NewGuid();
        db.Add(new ReferralCode
        {
            Id = codeId, TenantId = tenant, TenantCustomerId = referrerRoster, Code = "GIFT1234", CreatedAt = Start,
        });
        db.Add(new ReferralAttribution
        {
            Id = Guid.NewGuid(),
            TenantId = tenant,
            ReferrerTenantCustomerId = referrerRoster,
            RefereeTenantCustomerId = refereeRoster,
            ReferralCodeId = codeId,
            Status = ReferralAttributionStatus.Pending,
            CreatedAt = Start,
        });
        db.Add(new ServiceOffering
        {
            Id = serviceId, TenantId = tenant, Name = "Cut", PriceAmount = 100m, Currency = "NGN", DurationMinutes = 30,
            CreatedAt = Start, UpdatedAt = Start,
        });
        db.Add(new BookingRecord
        {
            Id = bookingId,
            TenantId = tenant,
            ServiceOfferingId = serviceId,
            CustomerId = refereePlatform,
            StartAt = Start,
            EndAt = Start.AddHours(1),
            Status = BookingStatus.Paid,
            PaidAt = Start,
            CreatedAt = Start,
            UpdatedAt = Start,
        });
        await db.SaveChangesAsync();

        var events = new DomainEventCollector();
        var qualify = new ReferralQualificationService(db, new Guard(), events, new FixedClock());
        Assert.True((await qualify.TryQualifyAndIssueRewardsForPaidBookingAsync(tenant, bookingId, "test")).IsSuccess);
        Assert.True((await qualify.TryQualifyAndIssueRewardsForPaidBookingAsync(tenant, bookingId, "test")).IsSuccess);
        await db.SaveChangesAsync();

        var attribution = await db.Set<ReferralAttribution>().SingleAsync();
        Assert.Equal(ReferralAttributionStatus.Rewarded, attribution.Status);
        Assert.Equal(40, (await db.Set<LoyaltyAccount>().SingleAsync(x => x.TenantCustomerId == referrerRoster)).PointsBalance);
        Assert.Equal(20, (await db.Set<LoyaltyAccount>().SingleAsync(x => x.TenantCustomerId == refereeRoster)).PointsBalance);
        var raised = events.TakeAll();
        Assert.Contains(raised, e => e is ReferralQualified);
        Assert.Contains(raised, e => e is ReferralRewardIssued);
    }

    private static AdeniDbContext NewDb() =>
        new(
            new DbContextOptionsBuilder<AdeniDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new TenantContext());
}
