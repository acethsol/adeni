namespace Adeni.Domain.Auditing;

public static class AuditActions
{
    public const string BusinessApproved = "business.approved";
    public const string BusinessRejected = "business.rejected";
    public const string BusinessVerificationSubmitted = "business.verification_submitted";
    public const string BusinessSuspended = "business.suspended";
    public const string ReviewHidden = "review.hidden";
    public const string VerificationBadgeGranted = "verification_badge.granted";
    public const string VerificationBadgeRevoked = "verification_badge.revoked";
    public const string SubscriptionWebhookReceived = "subscription.webhook_received";
    public const string AdminMetricsRead = "admin.metrics.read";
    public const string AdminTransactionsRead = "admin.transactions.read";
    public const string TenantNotificationPreferencesUpdated = "tenant.notification_preferences_updated";
    public const string BookingAccepted = "booking.accepted";
    public const string BookingRejected = "booking.rejected";
    public const string BookingCheckedIn = "booking.checked_in";
    public const string BookingCompleted = "booking.completed";
    public const string BookingMarkedPaid = "booking.marked_paid";
    public const string CustomerExported = "customer.exported";
    public const string CustomerDeleted = "customer.deleted";
    public const string CrossTenantDenied = "security.cross_tenant_denied";
    public const string MarketCreated = "market.created";
    public const string MarketUpdated = "market.updated";
    public const string MarketLiveToggled = "market.live_toggled";
    public const string StaffInviteCreated = "staff.invite_created";
    public const string StaffInviteAccepted = "staff.invite_accepted";
    public const string StaffInviteResent = "staff.invite_resent";
    public const string StaffInviteRevoked = "staff.invite_revoked";
    public const string StaffAccessRoleChanged = "staff.access_role_changed";
    public const string StaffAccessRevoked = "staff.access_revoked";
    public const string SubscriptionWebhookApplied = "subscription.webhook_applied";
    public const string PricingCatalogVersionCreated = "pricing_catalog.version_created";
    public const string PricingCatalogEntriesUpdated = "pricing_catalog.entries_updated";
    public const string PricingCatalogVersionActivated = "pricing_catalog.version_activated";

    public const string TenantCommercialBound = "tenant.commercial_bound";
    public const string TenantSettingsUpdated = "tenant.settings_updated";
    public const string TenantReportingExported = "tenant.reporting.exported";
    public const string AdminReportingRead = "admin.reporting.read";
    public const string AdminAuditLogsRead = "admin.audit_logs.read";

    public const string RetentionPackageCreated = "retention.package_created";
    public const string RetentionPackageUpdated = "retention.package_updated";
    public const string RetentionPackageDeactivated = "retention.package_deactivated";
    public const string RetentionPackageSold = "retention.package_sold";
    public const string RetentionPackageVoided = "retention.package_voided";
    public const string RetentionPackageExpired = "retention.package_expired";
    public const string RetentionMembershipPlanCreated = "retention.membership_plan_created";
    public const string RetentionMembershipPlanUpdated = "retention.membership_plan_updated";
    public const string RetentionMembershipPlanDeactivated = "retention.membership_plan_deactivated";
    public const string RetentionMembershipEnrolled = "retention.membership_enrolled";
    public const string RetentionMembershipFrozen = "retention.membership_frozen";
    public const string RetentionMembershipUnfrozen = "retention.membership_unfrozen";
    public const string RetentionMembershipCancelled = "retention.membership_cancelled";
    public const string RetentionMembershipSubscribed = "retention.membership_subscribed";
    public const string RetentionMembershipActivated = "retention.membership_activated";
    public const string RetentionMembershipRenewed = "retention.membership_renewed";
    public const string RetentionMembershipPaymentFailed = "retention.membership_payment_failed";
    public const string RetentionMembershipPastDue = "retention.membership_past_due";
    public const string RetentionMembershipExpired = "retention.membership_expired";
    public const string RetentionLoyaltyProgramSaved = "retention.loyalty_program_saved";
    public const string RetentionLoyaltyPointsAdjusted = "retention.loyalty_points_adjusted";
}
