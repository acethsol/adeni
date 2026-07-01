namespace Adeni.Domain.Tenancy;

public enum TenantStatus
{
    Draft = 0,
    PendingVerification = 1,
    Verified = 2,
    Rejected = 3,
    Suspended = 4
}
