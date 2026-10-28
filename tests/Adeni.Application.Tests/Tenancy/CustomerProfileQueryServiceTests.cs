namespace Adeni.Application.Tests.Tenancy;

using System.Text.Json;
using Adeni.Application.Auth;
using Adeni.Application.Booking;
using Adeni.Application.Finance;
using Adeni.Application.Messaging;
using Adeni.Application.Subscriptions;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Domain.Identity;
using Adeni.Domain.Subscriptions;

public sealed class CustomerProfileQueryServiceTests
{
    private readonly Guid tenant = Guid.NewGuid();
    private readonly Guid customer = Guid.NewGuid();
    private readonly Actor actor = new();
    private readonly Context context = new();
    private readonly Locations locations = new();
    private readonly Sources sources = new();
    private readonly Clock clock = new();

    private CustomerProfileQueryService Service()
    {
        actor.TenantId = tenant;
        return new(actor, context, locations, sources, sources, sources, new Cursor(), clock);
    }

    [Theory]
    [InlineData(PortalPermissionRoles.Owner, true, true)]
    [InlineData(PortalPermissionRoles.Manager, true, true)]
    [InlineData(PortalPermissionRoles.Receptionist, false, true)]
    public async Task Profile_UsesServerPermissionsWithoutCallingRestrictedSources(string role, bool finance, bool messages)
    {
        actor.Role = role;
        var result = await Service().GetProfileAsync(tenant, customer, "actor");
        Assert.True(result.IsSuccess);
        Assert.Equal(finance ? "available" : "restricted", result.Value!.Availability.Finance);
        Assert.Equal(messages ? "available" : "restricted", result.Value.Availability.Messaging);
        Assert.Equal(finance ? 1 : 0, sources.FinanceCalls);
        Assert.Equal(finance, result.Value.RecordedOnlinePaymentsByCurrency is not null);
        Assert.Equal("notImplemented", result.Value.Availability.Retention);
        Assert.Equal("notImplemented", result.Value.Availability.Preferences);
    }

    private sealed class PackageSummaries(Adeni.Application.Retention.CustomerPackageSummary? summary) : Adeni.Application.Retention.ICustomerPackageSummaryQuery
    {
        public int Calls { get; private set; }

        public Task<Adeni.Application.Retention.CustomerPackageSummary?> GetSummaryAsync(Guid tenantId, Guid tenantCustomerId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(summary);
        }
    }

    [Fact]
    public async Task Profile_WithPackagesEntitlement_ExposesRetentionSummary()
    {
        actor.Role = PortalPermissionRoles.Owner;
        context.Entitlements = context.Entitlements with { Packages = true };
        var summaries = new PackageSummaries(new(1, 2, ["Cut x3"]));
        actor.TenantId = tenant;
        var service = new CustomerProfileQueryService(actor, context, locations, sources, sources, sources, new Cursor(), clock, summaries);

        var result = await service.GetProfileAsync(tenant, customer, "actor");

        Assert.Equal("available", result.Value!.Availability.Retention);
        Assert.Equal(2, result.Value.Packages!.TotalRemainingCredits);
    }

    [Fact]
    public async Task Profile_WithoutPackagesEntitlement_ReportsNotEntitledAndSkipsQuery()
    {
        actor.Role = PortalPermissionRoles.Owner;
        context.Entitlements = context.Entitlements with { Packages = false };
        var summaries = new PackageSummaries(new(1, 2, ["Cut x3"]));
        actor.TenantId = tenant;
        var service = new CustomerProfileQueryService(actor, context, locations, sources, sources, sources, new Cursor(), clock, summaries);

        var result = await service.GetProfileAsync(tenant, customer, "actor");

        Assert.Equal("notEntitled", result.Value!.Availability.Retention);
        Assert.Null(result.Value.Packages);
        Assert.Equal(0, summaries.Calls);
    }
    [Theory]
    [InlineData(PortalPermissionRoles.Practitioner)]
    [InlineData(PortalPermissionRoles.Accountant)]
    [InlineData(PortalPermissionRoles.Ops)]
    public async Task Profile_WithoutFullBookingsPermission_DeniesBeforeReadingCustomer(string role)
    {
        actor.Role = role;
        var result = await Service().GetProfileAsync(tenant, customer, "actor");
        Assert.Equal(ErrorCodes.PermissionDenied, result.Error.Code);
        Assert.Equal(0, context.Calls);
        Assert.Equal(0, sources.BookingCalls);
    }

    [Fact]
    public async Task Profile_WrongTenant_DeniesBeforeSourceQueries()
    {
        var service = Service();
        actor.TenantId = Guid.NewGuid();
        Assert.True((await service.GetProfileAsync(tenant, customer, "actor")).IsFailure);
        Assert.Equal(0, context.Calls);
    }

    [Fact]
    public async Task Profile_UnlinkedWalkIn_ReportsUnavailableInsteadOfZeroMoney()
    {
        context.CustomerId = null;
        var result = await Service().GetProfileAsync(tenant, customer, "actor");
        Assert.Equal("notLinked", result.Value!.Availability.Finance);
        Assert.Equal("notLinked", result.Value.Availability.Messaging);
        Assert.Null(result.Value.RecordedOnlinePaymentsByCurrency);
        Assert.Equal(0, sources.FinanceCalls);
    }

    [Fact]
    public async Task Activity_LocationScope_IsPassedToSourcesAndOmitsUnscopedMessages()
    {
        locations.Id = Guid.NewGuid();
        var result = await Service().GetActivityAsync(tenant, customer, "actor");
        Assert.Equal(locations.Id, sources.LastScope!.LocationId);
        Assert.Equal("unscoped", result.Value!.Availability.Messaging);
        Assert.Equal(0, sources.MessageCalls);
    }

    [Fact]
    public async Task Activity_InvalidLocation_FailsBeforeSourceQueries()
    {
        locations.Fail = true;
        var result = await Service().GetActivityAsync(tenant, customer, "actor");
        Assert.True(result.IsFailure);
        Assert.Equal(0, sources.BookingCalls);
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 25)]
    public async Task Activity_MergesWithStableTiesAndKeepsPlanCapOnEveryPage(bool paid, int limit)
    {
        context.Entitlements = context.Entitlements with { CustomerRetentionAnalytics = paid };
        sources.BookingItems = [Item("a"), Item("c")];
        sources.FinanceItems = [Item("b")];
        var service = Service();
        var first = await service.GetActivityAsync(tenant, customer, "actor", pageSize: 1);
        Assert.Equal("c", Assert.Single(first.Value!.Items).Id);
        Assert.Equal(limit, sources.BookingLimit);
        var second = await service.GetActivityAsync(tenant, customer, "actor", pageSize: 1, cursor: first.Value.NextCursor);
        Assert.Equal("b", Assert.Single(second.Value!.Items).Id);
        Assert.Equal(first.Value.AsOf, second.Value.AsOf);
        Assert.Equal(limit, sources.BookingLimit);
        var third = await service.GetActivityAsync(tenant, customer, "actor", pageSize: 1, cursor: second.Value.NextCursor);
        Assert.Equal("a", Assert.Single(third.Value!.Items).Id);
        Assert.Null(third.Value.NextCursor);
    }

    [Fact]
    public async Task Activity_PolicyCustomerLocationOrActorChanges_RejectsCursor()
    {
        sources.BookingItems = [Item("a"), Item("b")];
        var service = Service();
        var cursor = (await service.GetActivityAsync(tenant, customer, "actor", pageSize: 1)).Value!.NextCursor;
        Assert.NotNull(cursor);
        Assert.True((await service.GetActivityAsync(tenant, Guid.NewGuid(), "actor", cursor: cursor)).IsFailure);
        actor.Id = Guid.NewGuid();
        Assert.True((await service.GetActivityAsync(tenant, customer, "actor", cursor: cursor)).IsFailure);
        actor.Id = Actor.OriginalId;
        context.Entitlements = context.Entitlements with { CustomerRetentionAnalytics = true };
        Assert.True((await service.GetActivityAsync(tenant, customer, "actor", cursor: cursor)).IsFailure);
        context.Entitlements = context.Entitlements with { CustomerRetentionAnalytics = false };
        locations.Id = Guid.NewGuid();
        Assert.True((await service.GetActivityAsync(tenant, customer, "actor", cursor: cursor)).IsFailure);
    }

    [Fact]
    public async Task Activity_CursorExpiresAndMalformedCursorFails()
    {
        sources.BookingItems = [Item("a"), Item("b")];
        var service = Service();
        var cursor = (await service.GetActivityAsync(tenant, customer, "actor", pageSize: 1)).Value!.NextCursor;
        clock.Now = clock.Now.AddHours(2);
        Assert.True((await service.GetActivityAsync(tenant, customer, "actor", cursor: cursor)).IsFailure);
        Assert.True((await service.GetActivityAsync(tenant, customer, "actor", cursor: "bad")).IsFailure);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task Activity_InvalidPageSize_FailsBeforeAuthorization(int size)
    {
        Assert.True((await Service().GetActivityAsync(tenant, customer, "actor", pageSize: size)).IsFailure);
        Assert.Equal(0, context.Calls);
    }

    [Fact]
    public async Task Activity_TruncationAndFailure_AreNotPretendedToBeEmptySuccess()
    {
        sources.Truncated = true;
        var service = Service();
        Assert.True((await service.GetActivityAsync(tenant, customer, "actor")).Value!.IsTruncated);
        sources.Throw = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetActivityAsync(tenant, customer, "actor"));
    }

    [Fact]
    public async Task Profile_BasicAnalyticsAndMessagingDisabled_DoNotExposeSources()
    {
        context.Entitlements = context.Entitlements with { BasicAnalytics = false, Messaging = false };
        var service = Service();
        var profile = (await service.GetProfileAsync(tenant, customer, "actor")).Value!;
        Assert.Equal("notEntitled", profile.Availability.Finance);
        Assert.Equal("notEntitled", profile.Availability.Messaging);
        Assert.Null(profile.VisitSummary);
        await service.GetActivityAsync(tenant, customer, "actor");
        Assert.Equal(0, sources.FinanceCalls);
        Assert.Equal(0, sources.MessageCalls);
    }

    private CustomerActivityItem Item(string id) => new(id, "bookingCreated", clock.Now.AddDays(-1), "booking", Guid.NewGuid());
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Actor : IPortalPermissionService
    {
        public static readonly Guid OriginalId = Guid.NewGuid();
        public Guid Id = OriginalId;
        public Guid TenantId;
        public string Role = PortalPermissionRoles.Owner;
        public Task<Result<PortalAccessSnapshot>> ResolveAsync(string sub, CancellationToken ct) =>
            Task.FromResult(Result.Success(new PortalAccessSnapshot(Id, TenantId, Role, PortalPermissionRoles.PermissionsFor(Role), null)));
        public Task<Result> EnsureAsync(string sub, string permission, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Context : ICustomerProfileContextQuery
    {
        public Guid? CustomerId = Guid.NewGuid();
        public int Calls;
        public TenantEntitlements Entitlements = JsonSerializer.Deserialize<TenantEntitlements>(
            """{"BasicAnalytics":true,"Messaging":true,"CustomerRetentionAnalytics":false}""")!;
        public Task<Result<CustomerProfileContext>> GetAsync(Guid tenant, Guid customer, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Result.Success(new CustomerProfileContext(customer, CustomerId, Entitlements)));
        }
    }
    private sealed class Locations : ITenantLocationScopeService
    {
        public Guid? Id;
        public bool Fail;
        public Task<Result<Guid?>> ResolveListLocationAsync(Guid tenant, Guid? location, CancellationToken ct) => Task.FromResult(
            Fail ? Result.Failure<Guid?>(Error.Validation("location")) : Result.Success(Id ?? location));
        public Task<Result<Guid?>> ValidateOptionalLocationIdAsync(Guid t, Guid? l, CancellationToken ct) => throw new NotSupportedException();
        public Task<Result<Guid>> ResolveAvailabilityLocationAsync(Guid t, Guid? l, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Cursor : ICustomerActivityCursorCodec
    {
        public string Encode(CustomerActivityCursor cursor) => JsonSerializer.Serialize(cursor);
        public CustomerActivityCursor? Decode(string cursor)
        {
            try { return JsonSerializer.Deserialize<CustomerActivityCursor>(cursor); }
            catch (JsonException) { return null; }
        }
    }
    private sealed class Sources : ICustomerBookingActivityQuery, ICustomerCommerceQuery, ICustomerCommunicationActivityQuery
    {
        public int BookingCalls, FinanceCalls, MessageCalls, BookingLimit;
        public bool Truncated, Throw;
        public CustomerProfileScope? LastScope;
        public IReadOnlyList<CustomerActivityItem> BookingItems = [], FinanceItems = [];
        public Task<CustomerBookingProfile> GetProfileAsync(CustomerProfileScope s, DateTimeOffset now, bool analytics, CancellationToken ct)
        {
            BookingCalls++;
            return Task.FromResult(new CustomerBookingProfile(analytics ? new(1, 0) : null, null));
        }
        public Task<CustomerActivityBatch> GetActivityAsync(CustomerProfileScope s, DateTimeOffset from, DateTimeOffset now, int limit, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (Throw) throw new InvalidOperationException();
            BookingCalls++; BookingLimit = limit; LastScope = s;
            return Task.FromResult(new CustomerActivityBatch(BookingItems, Truncated));
        }
        public Task<IReadOnlyList<CustomerCurrencyTotal>> GetTotalsAsync(CustomerProfileScope s, DateTimeOffset now, CancellationToken ct)
        { FinanceCalls++; return Task.FromResult<IReadOnlyList<CustomerCurrencyTotal>>([]); }
        Task<CustomerActivityBatch> ICustomerCommerceQuery.GetActivityAsync(CustomerProfileScope s, DateTimeOffset from, DateTimeOffset now, CancellationToken ct)
        { FinanceCalls++; return Task.FromResult(new CustomerActivityBatch(FinanceItems)); }
        Task<CustomerActivityBatch> ICustomerCommunicationActivityQuery.GetActivityAsync(CustomerProfileScope s, DateTimeOffset from, DateTimeOffset now, CancellationToken ct)
        { MessageCalls++; return Task.FromResult(new CustomerActivityBatch([])); }
    }
}
