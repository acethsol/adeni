namespace Adeni.Infrastructure.Calendar;

public sealed class CalendarOptions
{
    public string ApiBaseUrl { get; set; } = "";
    public string PortalBaseUrl { get; set; } = "";
    public string CurrentKeyVersion { get; set; } = "";
    public Dictionary<string, string> EncryptionKeys { get; set; } = [];
    public CalendarProviderOptions Google { get; set; } = new();
    public CalendarProviderOptions Microsoft { get; set; } = new();
    public CalendarProviderOptions Provider(string provider) => provider switch
    {
        "google" => Google,
        "microsoft" => Microsoft,
        _ => throw new CalendarProviderException("provider_invalid")
    };
    public string Callback(string provider) => ApiBaseUrl.TrimEnd('/') + "/api/v1/calendar/oauth/" + provider + "/callback";
    public bool IsValid()
    {
        if (!Google.Enabled && !Microsoft.Enabled) return true;
        static bool Origin(string value) => Uri.TryCreate(value, UriKind.Absolute, out var u) && u.Scheme == "https"
            && u.AbsolutePath == "/" && u.Query == "" && u.Fragment == "" && u.UserInfo == "";
        return Origin(ApiBaseUrl) && Origin(PortalBaseUrl)
            && CurrentKeyVersion.Length is > 0 and <= 32 && !CurrentKeyVersion.Contains('.')
            && EncryptionKeys.TryGetValue(CurrentKeyVersion, out var key) && ValidKey(key)
            && EncryptionKeys.All(x => x.Key.Length is > 0 and <= 32 && !x.Key.Contains('.') && ValidKey(x.Value))
            && new[] { Google, Microsoft }.Where(x => x.Enabled).All(x => !string.IsNullOrWhiteSpace(x.ClientId) && !string.IsNullOrWhiteSpace(x.ClientSecret));
    }
    private static bool ValidKey(string value) { try { return Convert.FromBase64String(value).Length == 32; } catch (FormatException) { return false; } }
}
public sealed class CalendarProviderOptions
{
    public bool Enabled { get; set; }
    public bool WriteBackEnabled { get; set; }
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public override string ToString() => "[Calendar provider configuration]";
}

/// <summary>Safe exception category only: never retain provider response bodies, URLs or inner exceptions.</summary>
public sealed class CalendarProviderException(string category, TimeSpan? retryAfter = null) : Exception(category)
{
    public string Category { get; } = category;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
