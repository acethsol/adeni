namespace Adeni.Infrastructure.Subscriptions;

using Adeni.Application.Subscriptions;
using Adeni.Domain.Booking;
using Adeni.Domain.Common;
using Adeni.Domain.Tenancy;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class EntitlementsService(
    AdeniDbContext dbContext,
    ITenantPricingResolver tenantPricingResolver,
    IPlanEntitlementsMatrix planEntitlementsMatrix) : IEntitlementsService
{
    public TenantEntitlements GetEntitlementsForTenant(Tenant tenant) =>
        planEntitlementsMatrix.ForSku(TenantBillingPolicy.ResolveEffectiveSku(tenant));

    public async Task<Result<TenantEntitlements>> GetEntitlementsForMemberAsync(
        Guid tenantId,
        string auth0Sub,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auth0Sub))
        {
            return Result.Failure<TenantEntitlements>(Error.Forbidden("Authentication is required."));
        }

        var hasAccess = await dbContext.BusinessUsers
            .AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Auth0Sub == auth0Sub, cancellationToken);

        if (!hasAccess)
        {
            return Result.Failure<TenantEntitlements>(Error.Forbidden("You do not have access to this business."));
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantEntitlements>(Error.NotFound("Business"));
        }

        return Result.Success(GetEntitlementsForTenant(tenant));
    }

    public async Task<int> GetBookingsUsedThisMonthAsync(
        Guid tenantId,
        Guid? locationId = null,
        CancellationToken cancellationToken = default)
    {
        var monthStart = new DateTimeOffset(
            DateTimeOffset.UtcNow.Year,
            DateTimeOffset.UtcNow.Month,
            1,
            0,
            0,
            0,
            TimeSpan.Zero);

        var query = dbContext.Bookings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.CreatedAt >= monthStart
                && x.Status != BookingStatus.Cancelled);

        return await query
            .FilterBookingsByLocation(locationId)
            .CountAsync(cancellationToken);
    }

    public Task<int> GetActiveLocationCountAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        dbContext.BusinessLocations
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);

    public Task<int> GetActivePractitionerCountAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        dbContext.StaffMembers
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId && x.IsActive, cancellationToken);

    public Task<Result> EnsureCanCreateBookingForTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default)
    {
        _ = GetEntitlementsForTenant(tenant);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> EnsureCanAddLocationForTenantAsync(
        Tenant tenant,
        int activeLocationCount,
        CancellationToken cancellationToken = default)
    {
        var entitlements = GetEntitlementsForTenant(tenant);
        if (entitlements.MaxLocations is null || activeLocationCount < entitlements.MaxLocations.Value)
        {
            return Task.FromResult(Result.Success());
        }

        return Task.FromResult(Result.Failure(ErrorCodes.LocationLimitReachedError(entitlements.MaxLocations.Value)));
    }

    public Task<Result> EnsureCanAddPractitionerForTenantAsync(
        Tenant tenant,
        int activePractitionerCount,
        CancellationToken cancellationToken = default)
    {
        var entitlements = GetEntitlementsForTenant(tenant);
        if (entitlements.MaxPractitioners is null || activePractitionerCount < entitlements.MaxPractitioners.Value)
        {
            return Task.FromResult(Result.Success());
        }

        return Task.FromResult(Result.Failure(ErrorCodes.PractitionerLimitReachedError(entitlements.MaxPractitioners.Value)));
    }

    public Task<Result> EnsureCanUseMessagingForTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default)
    {
        var entitlements = GetEntitlementsForTenant(tenant);
        return Task.FromResult(EnsureMessagingEntitled(entitlements));
    }

    public Task<Result> EnsureCanUseWaitlistForTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken = default)
    {
        var entitlements = GetEntitlementsForTenant(tenant);
        return Task.FromResult(
            entitlements.Waitlist
                ? Result.Success()
                : Result.Failure(ErrorCodes.WaitlistNotEntitledError()));
    }

    public async Task<SubscriptionUsageResponse> GetUsageForTenantAsync(
        Tenant tenant,
        Guid? bookingsLocationId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveSku = TenantBillingPolicy.ResolveEffectiveSku(tenant);
        var entitlements = planEntitlementsMatrix.ForSku(effectiveSku);
        var used = await GetBookingsUsedThisMonthAsync(tenant.Id, bookingsLocationId, cancellationToken);
        var locationsActive = await GetActiveLocationCountAsync(tenant.Id, cancellationToken);
        var practitionersActive = await GetActivePractitionerCountAsync(tenant.Id, cancellationToken);
        var contractSku = tenant.SubscriptionSku;
        var contractApi = SubscriptionSkuMapping.ToApiValue(contractSku);

        var country = await tenantPricingResolver.ResolvePrimaryBillingCountryAsync(
            tenant.Id,
            cancellationToken);
        var pricing = tenantPricingResolver.ResolveForTenant(tenant, country);

        return new SubscriptionUsageResponse(
            SubscriptionSkuMapping.ToApiValue(effectiveSku),
            entitlements,
            used,
            BookingsLimitThisMonth: null,
            locationsActive,
            practitionersActive,
            contractApi != SubscriptionSkuMapping.ToApiValue(effectiveSku) ? contractApi : null,
            TenantBillingStatusMapping.ToApiValue(tenant.BillingStatus),
            tenant.TrialEndsAt,
            tenant.PaidThrough,
            pricing.CatalogListPriceMajor,
            pricing.AgreedMonthlyAmountMajor ?? tenant.AgreedMonthlyAmount,
            pricing.EffectiveMonthlyAmountMajor,
            pricing.Currency,
            pricing.PricingCatalogVersion,
            pricing.BoundPricingCatalogVersion);
    }

    private static Result EnsureMessagingEntitled(TenantEntitlements entitlements) =>
        entitlements.Messaging
            ? Result.Success()
            : Result.Failure(ErrorCodes.MessagingNotEntitledError());
}
