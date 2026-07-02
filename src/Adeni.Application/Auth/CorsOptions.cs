namespace Adeni.Application.Auth;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}

public sealed record AuthSessionResponse(
    string? UserId,
    IReadOnlyCollection<string> Roles,
    Guid? TenantId,
    bool HasMfa);
