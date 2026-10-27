namespace Adeni.Infrastructure.Logging;

using Serilog.Events;

/// <summary>Hosting diagnostics run outside middleware and can otherwise record OAuth query strings.</summary>
public static class CalendarOAuthLogFilter
{
    public static bool ShouldExclude(LogEvent entry)
    {
        if (!entry.Properties.TryGetValue("SourceContext", out var source)
            || source is not ScalarValue { Value: string category }
            || !category.StartsWith("Microsoft.AspNetCore.Hosting.Diagnostics", StringComparison.Ordinal)) return false;
        return new[] { "Path", "RequestPath" }.Any(key => entry.Properties.TryGetValue(key, out var path)
            && path is ScalarValue { Value: string value } && value.StartsWith("/api/v1/calendar/oauth/", StringComparison.OrdinalIgnoreCase));
    }
}
