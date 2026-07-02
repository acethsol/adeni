namespace Adeni.Application.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    /// <summary>
    /// When true, registers OpenTelemetry + Azure Monitor when a connection string is available.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Azure Application Insights connection string.
    /// Falls back to APPLICATIONINSIGHTS_CONNECTION_STRING environment variable.
    /// </summary>
    public string? ConnectionString { get; set; }

    public string ServiceName { get; set; } = "adeni-api";
}
