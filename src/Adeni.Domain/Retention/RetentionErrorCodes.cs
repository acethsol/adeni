namespace Adeni.Domain.Retention;

using Adeni.Domain.Common;

/// <summary>Stable <c>retention.*</c> error codes (mirrored in <c>@adeni/shared</c> i18n).</summary>
public static class RetentionErrorCodes
{
    public const string PackagesNotEntitled = "entitlements.packages";
    public const string MembershipsNotEntitled = "entitlements.memberships";
    public const string MembershipPlanNotFound = "retention.membership_plan_not_found";
    public const string MembershipNotFound = "retention.membership_not_found";
    public const string MembershipPlanInactive = "retention.membership_plan_inactive";
    public const string MembershipAlreadyActive = "retention.membership_already_active";
    public const string MembershipInvalidState = "retention.membership_invalid_state";
    public const string MembershipFreezeNotAllowed = "retention.membership_freeze_not_allowed";
    public const string LoyaltyBasicNotEntitled = "entitlements.loyaltyBasic";
    public const string LoyaltyTiersNotEntitled = "entitlements.loyaltyTiers";
    public const string ReferralsNotEntitled = "entitlements.referrals";
    public const string CrossLocationRetentionNotEntitled = "entitlements.crossLocationRetention";
    public const string AdvancedRetentionAnalyticsNotEntitled = "entitlements.advancedRetentionAnalytics";
    public const string ReferralProgramNotConfigured = "retention.referral_program_not_configured";
    public const string LoyaltyProgramNotConfigured = "retention.loyalty_program_not_configured";
    public const string LoyaltyInsufficientPoints = "retention.loyalty_insufficient_points";
    public const string LoyaltyConflict = "retention.loyalty_conflict";
    public const string LoyaltyRedeemInvalid = "retention.loyalty_redeem_invalid";
    public const string LoyaltyProgramDisabled = "retention.loyalty_program_disabled";
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
    public const string RecurringBillingNotEntitled = "entitlements.recurringMembershipBilling";
    public const string OnlineSubscriptionUnavailable = "retention.online_subscription_unavailable";
    public const string MembershipRenewalNotDue = "retention.membership_renewal_not_due";
    public const string MembershipNotRedeemable = "retention.membership_not_redeemable";

    public static Error MembershipNotRedeemableError() =>
        new(MembershipNotRedeemable, "This membership's credits cannot be used right now.");

    /// <summary>Online recurring checkout is gated until tenant merchant settlement is live (Sprint 34); sandbox/stub only.</summary>
    public static Error OnlineSubscriptionUnavailableError() =>
        new(OnlineSubscriptionUnavailable, "Online membership billing is not available for this business yet.");

    public static Error RecurringBillingNotEntitledError() =>
        new(RecurringBillingNotEntitled, "Recurring membership billing is not included on your current plan.");

    public static Error MembershipRenewalNotDueError() =>
        new(MembershipRenewalNotDue, "This membership is not due for renewal yet, or cannot be renewed here.");

    /// <summary>Online checkout is gated until tenant merchant settlement is live (Sprint 34); sandbox/stub only.</summary>
    public static Error OnlinePurchaseUnavailableError() =>
        new(OnlinePurchaseUnavailable, "Online package purchase is not available for this business yet.");

    public static Error PackagesNotEntitledError() =>
        new(PackagesNotEntitled, "Prepaid packages are not included on your current plan.");

    public static Error MembershipsNotEntitledError() =>
        new(MembershipsNotEntitled, "Memberships are not included on your current plan.");

    public static Error LoyaltyBasicNotEntitledError() =>
        new(LoyaltyBasicNotEntitled, "Loyalty is not included on your current plan.");

    public static Error LoyaltyTiersNotEntitledError() =>
        new(LoyaltyTiersNotEntitled, "Loyalty tiers and rewards are not included on your current plan.");

    public static Error ReferralsNotEntitledError() =>
        new(ReferralsNotEntitled, "Referrals are not included on your current plan.");

    public static Error CrossLocationRetentionNotEntitledError() =>
        new(CrossLocationRetentionNotEntitled, "Cross-location package and membership redemption requires the Business plan.");

    public static Error AdvancedRetentionAnalyticsNotEntitledError() =>
        new(AdvancedRetentionAnalyticsNotEntitled, "Retention analytics are not included on your current plan.");

    public static Error ReferralProgramNotConfiguredError() =>
        new(ReferralProgramNotConfigured, "Set up the referral program before sharing codes.");

    public static Error LoyaltyProgramNotConfiguredError() =>
        new(LoyaltyProgramNotConfigured, "Set up the loyalty program before adjusting points.");

    public static Error LoyaltyInsufficientPointsError() =>
        new(LoyaltyInsufficientPoints, "That would take the customer's points balance below zero.");

    public static Error LoyaltyConflictError() =>
        new(LoyaltyConflict, "The points balance changed at the same time. Try again.");

    public static Error LoyaltyRedeemInvalidError(string? detail = null) =>
        new(LoyaltyRedeemInvalid, detail ?? "Those loyalty points cannot be applied to this booking.");

    public static Error LoyaltyProgramDisabledError() =>
        new(LoyaltyProgramDisabled, "The loyalty program is not enabled for this business.");

    public static Error MembershipPlanNotFoundError() =>
        new(MembershipPlanNotFound, "Membership plan was not found.");

    public static Error MembershipNotFoundError() =>
        new(MembershipNotFound, "Membership was not found.");

    public static Error MembershipPlanInactiveError() =>
        new(MembershipPlanInactive, "This membership plan is no longer offered.");

    public static Error MembershipAlreadyActiveError() =>
        new(MembershipAlreadyActive, "This customer already has a live membership on that plan.");

    public static Error MembershipInvalidStateError() =>
        new(MembershipInvalidState, "That change is not allowed for the membership's current status.");

    public static Error MembershipFreezeNotAllowedError() =>
        new(MembershipFreezeNotAllowed, "This membership plan does not allow freezing.");

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