namespace Adeni.Domain.Retention;

using Adeni.Domain.Tenancy;

/// <summary>Package instance sold to a roster customer (<c>TenantCustomer</c>). Definition fields are snapshotted.</summary>
public sealed class CustomerPackage : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid TenantCustomerId { get; set; }

    public Guid PackageDefinitionId { get; set; }

    public Guid? SoldAtLocationId { get; set; }

    public PackageSoldChannel SoldChannel { get; set; }

    public Guid? PaymentIntentId { get; set; }

    public CustomerPackageStatus Status { get; set; }

    public string NameSnapshot { get; set; } = string.Empty;

    public decimal ListPriceSnapshot { get; set; }

    public string CurrencySnapshot { get; set; } = "NGN";

    public PackageRedemptionScope RedemptionScopeSnapshot { get; set; }

    public string? RedemptionLocationIdsJson { get; set; }

    public string? Note { get; set; }

    public string SoldBy { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? ExpiredAt { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }

    public string? VoidedBy { get; set; }

    public string? VoidReason { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<BenefitBalance> Balances { get; set; } = new List<BenefitBalance>();

    /// <summary>True when redeemable right now (Active, not past expiry).</summary>
    public bool IsRedeemableAt(DateTimeOffset now) =>
        Status == CustomerPackageStatus.Active && (ExpiresAt is null || ExpiresAt > now);

    public override string ToString() => $"CustomerPackage {Id} {Status}";
}