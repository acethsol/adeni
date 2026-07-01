namespace Adeni.Domain.Tenancy;

public sealed class VerificationDocument : ITenantEntity
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public VerificationDocumentType DocumentType { get; set; }

    public string ReferenceNumber { get; set; } = string.Empty;

    public DateTimeOffset SubmittedAt { get; set; }

    public Tenant? Tenant { get; set; }
}
