namespace Adeni.Domain.Auditing;

public static class AuditActions
{
    public const string BusinessApproved = "business.approved";
    public const string BusinessRejected = "business.rejected";
    public const string BusinessVerificationSubmitted = "business.verification_submitted";
    public const string BusinessSuspended = "business.suspended";
    public const string ReviewHidden = "review.hidden";
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
}
