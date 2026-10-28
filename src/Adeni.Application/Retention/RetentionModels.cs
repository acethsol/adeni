namespace Adeni.Application.Retention;

using Adeni.Domain.Retention;

/// <summary>API snake_case codes for scope enums.</summary>
public static class RetentionScopeCodes
{
    public const string AllLocations = "all_locations";
    public const string PurchaseLocationOnly = "purchase_location_only";
    public const string SelectedLocations = "selected_locations";

    public static string ToApi(PackageRedemptionScope scope) =>
        scope switch
        {
            PackageRedemptionScope.PurchaseLocationOnly => PurchaseLocationOnly,
            PackageRedemptionScope.SelectedLocations => SelectedLocations,
            _ => AllLocations,
        };

    public static string ToApi(PackageSellScope scope) =>
        scope == PackageSellScope.SelectedLocations ? SelectedLocations : AllLocations;

    public static bool TryParseRedemption(string? value, out PackageRedemptionScope scope)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or AllLocations:
                scope = PackageRedemptionScope.AllLocations;
                return true;
            case PurchaseLocationOnly:
                scope = PackageRedemptionScope.PurchaseLocationOnly;
                return true;
            case SelectedLocations:
                scope = PackageRedemptionScope.SelectedLocations;
                return true;
            default:
                scope = default;
                return false;
        }
    }

    public static bool TryParseSell(string? value, out PackageSellScope scope)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or AllLocations:
                scope = PackageSellScope.AllLocations;
                return true;
            case SelectedLocations:
                scope = PackageSellScope.SelectedLocations;
                return true;
            default:
                scope = default;
                return false;
        }
    }

    public static string ToApi(CustomerPackageStatus status) =>
        status switch
        {
            CustomerPackageStatus.PendingPayment => "pending_payment",
            CustomerPackageStatus.Expired => "expired",
            CustomerPackageStatus.Voided => "voided",
            _ => "active",
        };

    public static string ToApi(PackageSoldChannel channel) =>
        channel switch
        {
            PackageSoldChannel.InPersonTransfer => "in_person_transfer",
            PackageSoldChannel.Online => "online",
            _ => "in_person_till",
        };
}

public sealed record PackageBenefitInput(Guid ServiceOfferingId, int QuantityGranted);

public sealed record PackageBenefitResponse(
    Guid Id,
    Guid ServiceOfferingId,
    string ServiceName,
    int QuantityGranted);

/// <summary>Create body; <c>Benefits</c> required, scopes default to all locations.</summary>
public sealed record CreatePackageDefinitionRequest(
    string Name,
    decimal ListPrice,
    string Currency,
    IReadOnlyList<PackageBenefitInput> Benefits,
    string? Description = null,
    int? DefaultExpiryMonths = null,
    string? RedemptionScope = null,
    string? SellScope = null,
    IReadOnlyList<Guid>? RedemptionLocationIds = null,
    IReadOnlyList<Guid>? SellLocationIds = null);

/// <summary>PATCH body — null leaves a field unchanged. Sold instances are never rewritten.</summary>
public sealed record UpdatePackageDefinitionRequest(
    string? Name = null,
    string? Description = null,
    decimal? ListPrice = null,
    string? Currency = null,
    int? DefaultExpiryMonths = null,
    string? RedemptionScope = null,
    string? SellScope = null,
    IReadOnlyList<Guid>? RedemptionLocationIds = null,
    IReadOnlyList<Guid>? SellLocationIds = null,
    IReadOnlyList<PackageBenefitInput>? Benefits = null);

public sealed record PackageDefinitionResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal ListPrice,
    string Currency,
    bool IsActive,
    int? DefaultExpiryMonths,
    int EffectiveExpiryMonths,
    string RedemptionScope,
    string SellScope,
    IReadOnlyList<Guid> RedemptionLocationIds,
    IReadOnlyList<Guid> SellLocationIds,
    IReadOnlyList<PackageBenefitResponse> Benefits,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PackageDefinitionListResponse(IReadOnlyList<PackageDefinitionResponse> Items);

/// <summary>Who is acting. <c>Role</c> is the portal RBAC role (owner / manager / …).</summary>
public sealed record RetentionActor(string Id, string Role)
{
    public bool IsOwner => string.Equals(Role, "owner", StringComparison.OrdinalIgnoreCase);
}

public static class SellPaymentMethods
{
    public const string Till = "till";
    public const string Transfer = "transfer";
}

public sealed record SellPackageRequest(
    Guid PackageDefinitionId,
    Guid BusinessLocationId,
    string PaymentMethod,
    string? Note = null);

public sealed record VoidCustomerPackageRequest(string? Reason = null);

public sealed record BenefitBalanceResponse(
    Guid Id,
    Guid ServiceOfferingId,
    string ServiceName,
    int Granted,
    int Used,
    int Reserved,
    int Forfeited,
    int Remaining,
    DateTimeOffset? ExpiresAt);

public sealed record CustomerPackageResponse(
    Guid Id,
    Guid TenantId,
    Guid TenantCustomerId,
    Guid PackageDefinitionId,
    string Name,
    decimal ListPrice,
    string Currency,
    string Status,
    string SoldChannel,
    Guid? SoldAtLocationId,
    string RedemptionScope,
    IReadOnlyList<Guid> RedemptionLocationIds,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? VoidedAt,
    string? VoidReason,
    string? Note,
    IReadOnlyList<BenefitBalanceResponse> Balances,
    string? BusinessName = null);

public sealed record CustomerPackageListResponse(IReadOnlyList<CustomerPackageResponse> Items);

public sealed record BenefitTransactionResponse(
    Guid Id,
    int Sequence,
    string Kind,
    int Quantity,
    int Delta,
    int ReservedAfter,
    int UsedAfter,
    int RemainingAfter,
    Guid BenefitBalanceId,
    Guid? BookingId,
    Guid? BusinessLocationId,
    string? Reason,
    DateTimeOffset OccurredAt);

public sealed record CustomerPackageLedgerResponse(IReadOnlyList<BenefitTransactionResponse> Items);

public sealed record RedeemableBenefitResponse(
    Guid CustomerPackageId,
    string PackageName,
    Guid BenefitBalanceId,
    Guid ServiceOfferingId,
    string ServiceName,
    int Remaining,
    DateTimeOffset? ExpiresAt);

public sealed record RedeemableBenefitListResponse(IReadOnlyList<RedeemableBenefitResponse> Items);

/// <summary>Booking payload: apply credits from a package benefit balance to this booking.</summary>
public sealed record AppliedBenefitRequest(
    Guid CustomerPackageId,
    Guid BenefitBalanceId,
    int Quantity = 1);

/// <summary>Everything Retention needs from Booking to validate a reserve (no Booking DbSets in Retention).</summary>
public sealed record ReserveBenefitsRequest(
    Guid TenantId,
    Guid PlatformCustomerId,
    Guid BookingId,
    Guid? BusinessLocationId,
    IReadOnlyList<Guid> BookedServiceOfferingIds,
    int GuestCount,
    IReadOnlyList<AppliedBenefitRequest> Benefits,
    string Actor,
    DateTimeOffset At);

public sealed record ReservedBenefit(
    Guid CustomerPackageId,
    Guid BenefitBalanceId,
    Guid ServiceOfferingId,
    int Quantity);

/// <summary>Tenant-owned customer's package summary for F1 profile (counts + names only).</summary>
public sealed record CustomerPackageSummary(
    int ActiveCount,
    int TotalRemainingCredits,
    IReadOnlyList<string> ActivePackageNames);

/// <summary>Per-service credit count already applied to a booking (price covered by package).</summary>
public sealed record CoveredBenefitService(Guid ServiceOfferingId, int Quantity);

public sealed record ServiceOfferingLookup(Guid Id, string Name, bool IsActive);