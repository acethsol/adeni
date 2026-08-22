namespace Adeni.Domain.Payments;

public enum PaymentIntentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Cancelled = 3,
}
