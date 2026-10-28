namespace Adeni.Application.Retention;

using Adeni.Domain.Common;

public interface IPackageCatalogService
{
    Task<Result<PackageDefinitionListResponse>> ListAsync(
        Guid tenantId,
        bool activeOnly,
        CancellationToken cancellationToken = default);

    Task<Result<PackageDefinitionResponse>> GetAsync(
        Guid tenantId,
        Guid packageDefinitionId,
        CancellationToken cancellationToken = default);

    Task<Result<PackageDefinitionResponse>> CreateAsync(
        Guid tenantId,
        RetentionActor actor,
        CreatePackageDefinitionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PackageDefinitionResponse>> UpdateAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid packageDefinitionId,
        UpdatePackageDefinitionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PackageDefinitionResponse>> DeactivateAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid packageDefinitionId,
        CancellationToken cancellationToken = default);
}

public interface ICustomerPackageService
{
    Task<Result<CustomerPackageListResponse>> ListForCustomerAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerPackageResponse>> GetAsync(
        Guid tenantId,
        Guid customerPackageId,
        CancellationToken cancellationToken = default);

    Task<Result<CustomerPackageLedgerResponse>> GetLedgerAsync(
        Guid tenantId,
        Guid customerPackageId,
        CancellationToken cancellationToken = default);

    /// <summary>In-person sale (till / transfer). <paramref name="idempotencyKey"/> is required.</summary>
    Task<Result<CustomerPackageResponse>> SellAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid tenantCustomerId,
        SellPackageRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Owner-only. Releases reservations and forfeits remaining credits (audited).</summary>
    Task<Result<CustomerPackageResponse>> VoidAsync(
        Guid tenantId,
        RetentionActor actor,
        Guid customerPackageId,
        VoidCustomerPackageRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Consumer view across businesses for the signed-in platform customer.</summary>
    Task<Result<CustomerPackageListResponse>> ListForPlatformCustomerAsync(
        string customerAuth0Sub,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The only Retention surface Booking may call. Reserve / release / finalize <b>stage</b> ledger and balance
/// changes on the caller's unit of work; the caller's single <c>SaveChanges</c> commits them atomically with the booking.
/// </summary>
public interface IBenefitRedemptionService
{
    Task<Result<IReadOnlyList<ReservedBenefit>>> TryReserveBenefitsAsync(
        ReserveBenefitsRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ReleaseReservationsAsync(
        Guid tenantId,
        Guid bookingId,
        string actor,
        string reason,
        CancellationToken cancellationToken = default);

    Task<Result> FinalizeRedemptionsAsync(
        Guid tenantId,
        Guid bookingId,
        string actor,
        CancellationToken cancellationToken = default);

    Task<Result<RedeemableBenefitListResponse>> ListRedeemableAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        Guid? businessLocationId,
        IReadOnlyList<Guid>? serviceOfferingIds,
        CancellationToken cancellationToken = default);

    /// <summary>Services whose price is covered by reserved / redeemed package credits on this booking.</summary>
    Task<IReadOnlyList<CoveredBenefitService>> GetCoveredServicesAsync(
        Guid tenantId,
        Guid bookingId,
        CancellationToken cancellationToken = default);

    /// <summary>True when a save failed because a benefit balance changed concurrently.</summary>
    bool IsBenefitConflict(Exception error);
}

/// <summary>Consumer / Discover surface: public catalog, online purchase and customer-side redeemable credits.</summary>
public interface IPackageStorefrontService
{
    /// <summary>Anonymous. Verified businesses only; empty when the plan has no <c>packages</c> entitlement.</summary>
    Task<Result<PublicPackageCatalogResponse>> GetPublicCatalogAsync(
        string slug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a Finance payment intent and a <c>PendingPayment</c> package. Credits are granted only by the
    /// idempotent <c>PaymentCompleted</c> handler. <paramref name="idempotencyKey"/> is required.
    /// </summary>
    Task<Result<PackagePurchaseResponse>> PurchaseAsync(
        string customerAuth0Sub,
        string slug,
        Guid packageDefinitionId,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>The signed-in customer's own redeemable credits at one business (booking UI).</summary>
    Task<Result<RedeemableBenefitListResponse>> ListRedeemableForCustomerAsync(
        string customerAuth0Sub,
        string slug,
        Guid? businessLocationId,
        IReadOnlyList<Guid>? serviceOfferingIds,
        CancellationToken cancellationToken = default);
}

/// <summary>Activates <c>PendingPayment</c> packages after a confirmed payment (idempotent).</summary>
public interface IPackagePurchaseActivation
{
    Task<bool> ActivateForPaymentAsync(
        Guid tenantId,
        Guid paymentIntentId,
        decimal amount,
        string currency,
        CancellationToken cancellationToken = default);

    /// <summary>Safety net: re-checks pending online packages whose payment already completed.</summary>
    Task<int> ReconcilePendingAsync(int batchSize, CancellationToken cancellationToken = default);
}

/// <summary>Thin wrapper over <c>IEntitlementsService</c> for the <c>packages</c> plan flag.</summary>
public interface IRetentionEntitlementGuard
{
    Task<Result> EnsurePackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<bool> IsPackagesEntitledAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>Catalog service lookup implemented by Booking infrastructure (Retention never reads Booking DbSets).</summary>
public interface IRetentionServiceLookup
{
    Task<IReadOnlyList<ServiceOfferingLookup>> FindAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> serviceOfferingIds,
        CancellationToken cancellationToken = default);
}

/// <summary>Scheduled forfeiture: expires Active packages past <c>ExpiresAt</c> (releases reservations, forfeits remainder).</summary>
public interface IPackageExpiryService
{
    Task<int> ExpireDuePackagesAsync(int batchSize, CancellationToken cancellationToken = default);
}

/// <summary>F1 customer profile read port (counts / names only).</summary>
public interface ICustomerPackageSummaryQuery
{
    Task<CustomerPackageSummary?> GetSummaryAsync(
        Guid tenantId,
        Guid tenantCustomerId,
        CancellationToken cancellationToken = default);
}