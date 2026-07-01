namespace Adeni.Domain.Auditing;

public static class AuditActions
{
    public const string BusinessApproved = "business.approved";
    public const string BusinessRejected = "business.rejected";
    public const string BusinessSuspended = "business.suspended";
    public const string ReviewHidden = "review.hidden";
    public const string CustomerExported = "customer.exported";
    public const string CustomerDeleted = "customer.deleted";
    public const string CrossTenantDenied = "security.cross_tenant_denied";
}
