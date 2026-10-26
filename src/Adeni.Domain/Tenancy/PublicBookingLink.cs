namespace Adeni.Domain.Tenancy;

public sealed class PublicBookingLink : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public string Slug { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
