namespace Adeni.Application.Markets;

public sealed class MarketOptions
{
    public const string SectionName = "Market";

    /// <summary>
    /// IANA or platform time zone id used when a business has no override.
    /// Set per deployment (e.g. launch market), not hard-coded in application logic.
    /// </summary>
    public string DefaultTimeZoneId { get; set; } = "UTC";
}
