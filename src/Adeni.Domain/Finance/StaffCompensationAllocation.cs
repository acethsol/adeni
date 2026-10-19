namespace Adeni.Domain.Finance;

using Adeni.Domain.Tenancy;

public enum StaffCompensationType
{
    Tip = 1,
    Commission = 2,
}

public sealed class StaffCompensationAllocation : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid StaffMemberId { get; set; }

    public Guid? BookingId { get; set; }

    public Guid? PaymentIntentId { get; set; }

    public StaffCompensationType Type { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "NGN";

    public DateTimeOffset CreatedAt { get; set; }
}
