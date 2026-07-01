namespace Adeni.Application.Auth;

public sealed class Auth0Options
{
    public const string SectionName = "Auth0";

    public bool Enabled { get; set; }

    public string Domain { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public bool RequireMfaForAdmin { get; set; } = true;
}
