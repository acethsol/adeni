namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Auth;
using Adeni.Application.Retention;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public sealed class LoyaltyCustomerService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    ILoyaltyRedemptionService redemption,
    TimeProvider clock) : ILoyaltyCustomerService
{
    public async Task<Result<CustomerLoyaltyListResponse>> ListForPlatformCustomerAsync(
        string customerAuth0Sub,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<CustomerLoyaltyListResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        var customerId = await db.Customers.AsNoTracking()
            .Where(x => x.Auth0Sub == customerAuth0Sub)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (customerId is null)
        {
            return Result.Success(new CustomerLoyaltyListResponse([]));
        }

        var links = await db.TenantCustomers.AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .Select(x => new { x.TenantId, x.Id })
            .ToListAsync(cancellationToken);
        if (links.Count == 0)
        {
            return Result.Success(new CustomerLoyaltyListResponse([]));
        }

        var tenantIds = links.Select(x => x.TenantId).Distinct().ToArray();
        var entitledTenants = new List<Guid>();
        foreach (var tenantId in tenantIds)
        {
            if (await entitlements.IsLoyaltyBasicEntitledAsync(tenantId, cancellationToken))
            {
                entitledTenants.Add(tenantId);
            }
        }

        if (entitledTenants.Count == 0)
        {
            return Result.Success(new CustomerLoyaltyListResponse([]));
        }

        var programs = await db.Set<LoyaltyProgram>().AsNoTracking()
            .Where(x => entitledTenants.Contains(x.TenantId))
            .ToDictionaryAsync(x => x.TenantId, cancellationToken);

        var tenantCustomerIds = links.Where(x => entitledTenants.Contains(x.TenantId)).Select(x => x.Id).ToArray();
        var accounts = await db.Set<LoyaltyAccount>().AsNoTracking()
            .Where(x => tenantCustomerIds.Contains(x.TenantCustomerId))
            .ToDictionaryAsync(x => x.TenantCustomerId, cancellationToken);

        var names = await db.Tenants.AsNoTracking()
            .Where(x => entitledTenants.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var slugRows = await db.BusinessLocations.AsNoTracking()
            .Where(x => entitledTenants.Contains(x.TenantId) && x.IsActive)
            .Select(x => new { x.TenantId, x.Slug, x.IsPrimary })
            .ToListAsync(cancellationToken);
        var slugs = slugRows
            .GroupBy(x => x.TenantId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsPrimary).Select(x => x.Slug).First());

        var items = new List<CustomerLoyaltyAtBusinessResponse>();
        foreach (var link in links.Where(x => entitledTenants.Contains(x.TenantId)))
        {
            programs.TryGetValue(link.TenantId, out var program);
            accounts.TryGetValue(link.Id, out var account);
            var balance = account?.PointsBalance ?? 0;
            if (program is null && balance == 0)
            {
                continue;
            }

            items.Add(new CustomerLoyaltyAtBusinessResponse(
                link.TenantId,
                names.GetValueOrDefault(link.TenantId),
                slugs.GetValueOrDefault(link.TenantId),
                balance,
                program?.IsEnabled ?? false,
                program?.Currency ?? "NGN",
                program?.RedeemPoints ?? 100,
                program?.RedeemValue ?? 0m,
                program?.MinRedeemPoints ?? 0));
        }

        _ = clock;
        return Result.Success(new CustomerLoyaltyListResponse(items.OrderByDescending(x => x.PointsBalance).ToArray()));
    }

    public async Task<Result<CustomerLoyaltyAtBusinessResponse>> GetAtBusinessAsync(
        string customerAuth0Sub,
        string businessSlug,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<CustomerLoyaltyAtBusinessResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        var tenantId = await db.BusinessLocations.AsNoTracking()
            .Where(x => x.Slug == businessSlug && x.IsActive)
            .Select(x => (Guid?)x.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
        if (tenantId is null)
        {
            return Result.Failure<CustomerLoyaltyAtBusinessResponse>(Error.NotFound("Business"));
        }

        var entitled = await entitlements.EnsureLoyaltyBasicEntitledAsync(tenantId.Value, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<CustomerLoyaltyAtBusinessResponse>(entitled.Error);
        }

        var customerId = await db.Customers.AsNoTracking()
            .Where(x => x.Auth0Sub == customerAuth0Sub)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (customerId is null)
        {
            return Result.Success(new CustomerLoyaltyAtBusinessResponse(
                tenantId.Value, null, businessSlug, 0, false, "NGN", 100, 0m, 0));
        }

        var tenantCustomerId = await db.TenantCustomers.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerId == customerId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var program = await db.Set<LoyaltyProgram>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        var balance = 0;
        if (tenantCustomerId is not null)
        {
            balance = await db.Set<LoyaltyAccount>().AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.TenantCustomerId == tenantCustomerId)
                .Select(x => x.PointsBalance)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var name = await db.Tenants.AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return Result.Success(new CustomerLoyaltyAtBusinessResponse(
            tenantId.Value,
            name,
            businessSlug,
            balance,
            program?.IsEnabled ?? false,
            program?.Currency ?? "NGN",
            program?.RedeemPoints ?? 100,
            program?.RedeemValue ?? 0m,
            program?.MinRedeemPoints ?? 0));
    }

    public async Task<Result<LoyaltyRedeemQuoteResponse>> QuoteAtBusinessAsync(
        string customerAuth0Sub,
        string businessSlug,
        int pointsToRedeem,
        decimal payableAfterPackageCredits,
        string currency,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<LoyaltyRedeemQuoteResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        var tenantId = await VerifiedLocationQueries.ResolveTenantIdBySlugAsync(
            db, businessSlug.Trim().ToLowerInvariant(), cancellationToken);
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<LoyaltyRedeemQuoteResponse>(Error.NotFound("Business"));
        }

        var customerId = await db.Customers.AsNoTracking()
            .Where(x => x.Auth0Sub == customerAuth0Sub)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (customerId is null)
        {
            return Result.Failure<LoyaltyRedeemQuoteResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        return await redemption.QuoteAsync(
            tenantId, customerId.Value, pointsToRedeem, payableAfterPackageCredits, currency, cancellationToken);
    }
}
