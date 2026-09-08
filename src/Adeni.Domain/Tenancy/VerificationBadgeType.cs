namespace Adeni.Domain.Tenancy;

public enum VerificationBadgeType
{
    Phone = 0,
    Cac = 1,
    Address = 2,
    License = 3,
}

public enum VerificationBadgeStatus
{
    Pending = 0,
    Granted = 1,
    Revoked = 2,
}
