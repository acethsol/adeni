namespace Adeni.Domain.Identity;

public sealed class Customer
{
    public Guid Id { get; set; }

    public string Auth0Sub { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
