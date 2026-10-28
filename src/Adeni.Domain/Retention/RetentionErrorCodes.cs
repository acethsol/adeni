namespace Adeni.Domain.Retention;

using Adeni.Domain.Common;

/// <summary>Stable <c>retention.*</c> error codes (mirrored in <c>@adeni/shared</c> i18n).</summary>
public static class RetentionErrorCodes
{
    public const string PackagesNotEntitled = "entitlements.packages";
    public const string PackageNotFound = "retention.package_not_found";
    public const string PackageInactive = "retention.package_inactive";
    public const string PackageNotActive = "retention.package_not_active";
    public const string PackageExpired = "retention.package_expired";
    public const string BenefitNotFound = "retention.benefit_not_found";
    public const string BenefitInsufficient = "retention.benefit_insufficient";
    public const string BenefitServiceMismatch = "retention.benefit_service_mismatch";
    public const string BenefitConflict = "retention.benefit_conflict";
    public const string ScopeLocationDenied = "retention.scope_location_denied";
    public const string SellLocationDenied = "retention.sell_location_denied";
    public const string IdempotencyKeyRequired = "retention.idempotency_key_required";
    public const string IdempotencyConflict = "retention.idempotency_conflict";
    public const string OwnerRequired = "retention.owner_required";
    public const string Validation = "retention.validation";
    public const string OnlinePurchaseUnavailable = "retention.online_purchase_unavailable";

    /// <summary>Online checkout is gated until tenant merchant settlement is live (Sprint 34); sandbox/stub only.</summary>
    public static Error OnlinePurchaseUnavailableError() =>
        new(OnlinePurchaseUnavailable, "Online package purchase is not available for this business yet.");

    public static Error PackagesNotEntitledError() =>
        new(PackagesNotEntitled, "Prepaid packages are not included on your current plan.");

    public static Error PackageNotFoundError() =>
        new(PackageNotFound, "Package was not found.");

    public static Error PackageInactiveError() =>
        new(PackageInactive, "This package is no longer for sale.");

    public static Error PackageNotActiveError() =>
        new(PackageNotActive, "This package is not active.");

    public static Error PackageExpiredError() =>
        new(PackageExpired, "This package has expired.");

    public static Error BenefitNotFoundError() =>
        new(BenefitNotFound, "That package benefit was not found.");

    public static Error BenefitInsufficientError() =>
        new(BenefitInsufficient, "Not enough package credits remaining.");

    public static Error BenefitServiceMismatchError() =>
        new(BenefitServiceMismatch, "That package credit does not cover the booked service.");

    public static Error BenefitConflictError() =>
        new(BenefitConflict, "Package credits changed at the same time. Try again.");

    public static Error ScopeLocationDeniedError() =>
        new(ScopeLocationDenied, "This package cannot be redeemed at the booking location.");

    public static Error SellLocationDeniedError() =>
        new(SellLocationDenied, "This package cannot be sold at that location.");

    public static Error IdempotencyKeyRequiredError() =>
        new(IdempotencyKeyRequired, "An Idempotency-Key header is required.");

    public static Error IdempotencyConflictError() =>
        new(IdempotencyConflict, "That Idempotency-Key was already used for a different request.");

    public static Error OwnerRequiredError() =>
        new(OwnerRequired, "Only the business owner can do that.");

    public static Error ValidationError(string detail) =>
        new(Validation, detail);
}