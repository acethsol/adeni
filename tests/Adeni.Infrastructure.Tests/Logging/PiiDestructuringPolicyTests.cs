namespace Adeni.Infrastructure.Tests.Logging;

using Adeni.Application.Security;
using Adeni.Infrastructure.Logging;
using Serilog.Core;
using Serilog.Events;

public sealed class PiiDestructuringPolicyTests
{
    [Fact]
    public void TryDestructure_masks_pii_log_value()
    {
        var policy = new PiiDestructuringPolicy();
        var factory = new FixedPropertyValueFactory();

        var success = policy.TryDestructure(
            new PiiLogValue("user@example.com", PiiKind.Email),
            factory,
            out var result);

        Assert.True(success);
        Assert.Equal("u***@example.com", ((ScalarValue)result!).Value);
    }

    [Fact]
    public void TryDestructure_ignores_non_pii_values()
    {
        var policy = new PiiDestructuringPolicy();
        var factory = new FixedPropertyValueFactory();

        var success = policy.TryDestructure("plain", factory, out _);

        Assert.False(success);
    }

    private sealed class FixedPropertyValueFactory : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects = false) =>
            new ScalarValue(value);

        public LogEventPropertyValue CreatePropertyValue(string? value) =>
            new ScalarValue(value);
    }
}
