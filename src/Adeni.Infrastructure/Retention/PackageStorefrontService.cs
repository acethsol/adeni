namespace Adeni.Infrastructure.Retention;

using Adeni.Application.Common;
using Adeni.Application.Finance;
using Adeni.Application.Identity;
using Adeni.Application.Retention;
using Adeni.Application.Tenancy;
using Adeni.Domain.Common;
using Adeni.Domain.Retention;
using Adeni.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Discover storefront for prepaid packages. Online purchase is orchestration only: it starts a Finance payment
/// intent and a <c>PendingPayment</c> package; credits are granted by the idempotent <c>PaymentCompleted</c> handler.
/// Mirrors the F3 commerce guard — no live money until Sprint 34 merchant settlement exists (stub/sandbox only).
/// </summary>
public sealed class PackageStorefrontService(
    AdeniDbContext db,
    IRetentionEntitlementGuard entitlements,
    IPaymentOrchestrator payments,
    IPaymentProvider provider,
    ITenantCustomerService tenantCustomers,
    IBenefitRedemptionService redemption,
    IHostEnvironment environment,
    TimeProvider clock,
    ICustomerTrustSyncService customerTrustSync,
    ICustomerVerificationPolicy customerVerificationPolicy) : IPackageStorefrontService
{
    private const string PurchaseKind = "purchase";

    // No live fallback to platform-only provider charges: Sprint 34 must supply merchant settlement first.
    private bool OnlinePurchaseEnabled =>
        (environment.IsDevelopment() || environment.EnvironmentName == "Testing") && provider.ProviderName == "Stub";

    public async Task<Result<PublicPackageCatalogResponse>> GetPublicCatalogAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var tenantId = await ResolveTenantAsync(slug, cancellationToken);
        if (tenantId == Guid.Empty || !await entitlements.IsPackagesEntitledAsync(tenantId, cancellationToken))
        {
            return Result.Success(new PublicPackageCatalogResponse([], false));
        }

        var definitions = await db.Set<PackageDefinition>().AsNoTracking()
            .Include(x => x.Benefits)
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.ListPrice).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        var locations = await ActiveLocationIdsAsync(tenantId, cancellationToken);

        var items = definitions
            .Where(x => PickSellLocation(x, locations) is not null)
            .Select(x => new PublicPackageResponse(
                x.Id,
                x.Name,
                x.Description,
                x.ListPrice,
                x.Currency,
                x.EffectiveExpiryMonths,
                RetentionScopeCodes.ToApi(x.RedemptionScope),
                x.Benefits.OrderBy(b => b.SortOrder)
                    .Select(b => new PackageBenefitResponse(b.Id, b.ServiceOfferingId, b.ServiceName, b.QuantityGranted))
                    .ToArray()))
            .ToArray();

        return Result.Success(new PublicPackageCatalogResponse(items, OnlinePurchaseEnabled));
    }

    public async Task<Result<PackagePurchaseResponse>> PurchaseAsync(
        string customerAuth0Sub,
        string slug,
        Guid packageDefinitionId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<PackagePurchaseResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        await customerTrustSync.SyncAsync(customerAuth0Sub, null, cancellationToken);
        var verification = await customerVerificationPolicy.RequireEmailAndPhoneVerifiedAsync(
            customerAuth0Sub,
            null,
            cancellationToken);
        if (verification.IsFailure)
        {
            return Result.Failure<PackagePurchaseResponse>(verification.Error);
        }

        var key = IdempotencyKeyNormalizer.Normalize(idempotencyKey);
        if (key is null)
        {
            return Result.Failure<PackagePurchaseResponse>(RetentionErrorCodes.IdempotencyKeyRequiredError());
        }

        var tenantId = await ResolveTenantAsync(slug, cancellationToken);
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<PackagePurchaseResponse>(RetentionErrorCodes.PackageNotFoundError());
        }

        var entitled = await entitlements.EnsurePackagesEntitledAsync(tenantId, cancellationToken);
        if (entitled.IsFailure)
        {
            return Result.Failure<PackagePurchaseResponse>(entitled.Error);
        }

        if (!OnlinePurchaseEnabled)
        {
            return Result.Failure<PackagePurchaseResponse>(RetentionErrorCodes.OnlinePurchaseUnavailableError());
        }

        var customerId = await db.Customers.AsNoTracking()
            .Where(x => x.Auth0Sub == customerAuth0Sub)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (customerId is null)
        {
            return Result.Failure<PackagePurchaseResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        var keyHash = RetentionHash.KeyHash(tenantId, PurchaseKind, customerAuth0Sub, key);
        var fingerprint = RetentionHash.Fingerprint(packageDefinitionId, customerId);

        var replay = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay.Value;
        }

        var definition = await db.Set<PackageDefinition>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == packageDefinitionId, cancellationToken);
        if (definition is null)
        {
            return Result.Failure<PackagePurchaseResponse>(RetentionErrorCodes.PackageNotFoundError());
        }

        if (!definition.IsActive)
        {
            return Result.Failure<PackagePurchaseResponse>(RetentionErrorCodes.PackageInactiveError());
        }

        if (definition.ListPrice <= 0)
        {
            return Result.Failure<PackagePurchaseResponse>(
                RetentionErrorCodes.ValidationError("Free packages cannot be purchased online."));
        }

        var saleLocation = PickSellLocation(definition, await ActiveLocationIdsAsync(tenantId, cancellationToken));
        if (saleLocation is null)
        {
            return Result.Failure<PackagePurchaseResponse>(RetentionErrorCodes.SellLocationDeniedError());
        }

        var tenantCustomerId = await db.TenantCustomers.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CustomerId == customerId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (tenantCustomerId is null)
        {
            // Roster row is created on first purchase, same as the first booking would.
            var ensured = await tenantCustomers.EnsureFromBookingAsync(tenantId, customerId.Value, cancellationToken);
            if (ensured.IsFailure)
            {
                return Result.Failure<PackagePurchaseResponse>(ensured.Error);
            }

            tenantCustomerId = ensured.Value!.TenantCustomerId;
        }

        var now = clock.GetUtcNow();
        var package = new CustomerPackage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TenantCustomerId = tenantCustomerId.Value,
            PackageDefinitionId = definition.Id,
            SoldAtLocationId = saleLocation,
            SoldChannel = PackageSoldChannel.Online,
            Status = CustomerPackageStatus.PendingPayment,
            NameSnapshot = definition.Name,
            ListPriceSnapshot = definition.ListPrice,
            CurrencySnapshot = definition.Currency,
            RedemptionScopeSnapshot = definition.RedemptionScope,
            RedemptionLocationIdsJson = definition.RedemptionLocationIdsJson,
            SoldBy = $"customer:{customerId}",
            CreatedAt = now,
        };
        db.Add(package);
        db.Add(new BenefitOperation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            KeyHash = keyHash,
            Fingerprint = fingerprint,
            Kind = PurchaseKind,
            Actor = package.SoldBy,
            ResultCustomerPackageId = package.Id,
            OccurredAt = now,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race on the idempotency key: surface the winner's result.
            db.ChangeTracker.Clear();
            var winner = await ReplayAsync(tenantId, keyHash, fingerprint, cancellationToken);
            return winner ?? Result.Failure<PackagePurchaseResponse>(RetentionErrorCodes.BenefitConflictError());
        }

        return await EnsureCheckoutAsync(package, cancellationToken);
    }

    public async Task<Result<RedeemableBenefitListResponse>> ListRedeemableForCustomerAsync(
        string customerAuth0Sub,
        string slug,
        Guid? businessLocationId,
        IReadOnlyList<Guid>? serviceOfferingIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerAuth0Sub))
        {
            return Result.Failure<RedeemableBenefitListResponse>(ErrorCodes.CustomerAuthRequiredError());
        }

        var tenantId = await ResolveTenantAsync(slug, cancellationToken);
        if (tenantId == Guid.Empty)
        {
            return Result.Success(new RedeemableBenefitListResponse([]));
        }

        // Strictly the signed-in customer's own roster rows at this business.
        var tenantCustomerIds = await (
            from tc in db.TenantCustomers.AsNoTracking()
            join c in db.Customers.AsNoTracking() on tc.CustomerId equals c.Id
            where tc.TenantId == tenantId && c.Auth0Sub == customerAuth0Sub
            select tc.Id).ToListAsync(cancellationToken);

        var items = new List<RedeemableBenefitResponse>();
        foreach (var tenantCustomerId in tenantCustomerIds)
        {
            var result = await redemption.ListRedeemableAsync(
                tenantId, tenantCustomerId, businessLocationId, serviceOfferingIds, cancellationToken);
            if (result.IsFailure)
            {
                return result;
            }

            items.AddRange(result.Value!.Items);
        }

        return Result.Success(new RedeemableBenefitListResponse(items));
    }

    private async Task<Result<PackagePurchaseResponse>> EnsureCheckoutAsync(
        CustomerPackage package, CancellationToken cancellationToken)
    {
        if (package.Status == CustomerPackageStatus.Active && package.PaymentIntentId is { } paidIntent)
        {
            return Result.Success(new PackagePurchaseResponse(package.Id, paidIntent, "active", null));
        }

        if (package.Status != CustomerPackageStatus.PendingPayment)
        {
            return Result.Failure<PackagePurchaseResponse>(RetentionErrorCodes.PackageNotActiveError());
        }

        if (package.PaymentIntentId is { } existing)
        {
            var current = await payments.GetAsync(existing, cancellationToken);
            return current.IsFailure
                ? Result.Failure<PackagePurchaseResponse>(current.Error)
                : Result.Success(new PackagePurchaseResponse(
                    package.Id, existing, "pending_payment", EmptyToNull(current.Value!.CheckoutUrl)));
        }

        // Deterministic key: retrying after a failed init returns the same intent, never a second charge.
        var intent = await payments.InitializeAsync(
            new InitializePaymentRequest(
                package.TenantId,
                null,
                package.ListPriceSnapshot,
                package.CurrencySnapshot,
                "link",
                $"Package: {package.NameSnapshot}"),
            $"retention:package:{package.Id:N}",
            cancellationToken);
        if (intent.IsFailure)
        {
            return Result.Failure<PackagePurchaseResponse>(intent.Error);
        }

        package.PaymentIntentId = intent.Value!.Id;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Result.Failure<PackagePurchaseResponse>(RetentionErrorCodes.BenefitConflictError());
        }

        return Result.Success(new PackagePurchaseResponse(
            package.Id, intent.Value.Id, "pending_payment", EmptyToNull(intent.Value.CheckoutUrl)));
    }

    private async Task<Result<PackagePurchaseResponse>?> ReplayAsync(
        Guid tenantId, string keyHash, string fingerprint, CancellationToken cancellationToken)
    {
        var operation = await db.Set<BenefitOperation>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.KeyHash == keyHash, cancellationToken);
        if (operation is null)
        {
            return null;
        }

        if (operation.Fingerprint != fingerprint || operation.ResultCustomerPackageId is null)
        {
            return Result.Failure<PackagePurchaseResponse>(RetentionErrorCodes.IdempotencyConflictError());
        }

        var package = await db.Set<CustomerPackage>()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == operation.ResultCustomerPackageId, cancellationToken);
        return package is null
            ? Result.Failure<PackagePurchaseResponse>(RetentionErrorCodes.PackageNotFoundError())
            : await EnsureCheckoutAsync(package, cancellationToken);
    }

    private Task<Guid> ResolveTenantAsync(string slug, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(slug)
            ? Task.FromResult(Guid.Empty)
            : VerifiedLocationQueries.ResolveTenantIdBySlugAsync(db, slug.Trim().ToLowerInvariant(), cancellationToken);

    private async Task<IReadOnlyList<Guid>> ActiveLocationIdsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await db.BusinessLocations.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

    /// <summary>First active location (primary first) the package may be sold at; online sales have no till location.</summary>
    private static Guid? PickSellLocation(PackageDefinition definition, IReadOnlyList<Guid> activeLocations)
    {
        var allowed = LocationIdList.Parse(definition.SellLocationIdsJson);
        foreach (var id in activeLocations)
        {
            if (PackageScopePolicy.CanSellAt(definition.SellScope, allowed, id))
            {
                return id;
            }
        }

        return null;
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
