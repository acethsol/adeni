namespace Adeni.Infrastructure.Logging;

using Adeni.Application.Security;
using Serilog.Core;
using Serilog.Events;

public sealed class PiiDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue result)
    {
        result = null!;

        if (value is not PiiLogValue pii)
        {
            return false;
        }

        var sanitized = PiiMasker.SanitizeLogValue(pii.Value, pii.Kind);
        result = new ScalarValue(sanitized);
        return true;
    }
}

public sealed record PiiLogValue(string? Value, PiiKind Kind);
