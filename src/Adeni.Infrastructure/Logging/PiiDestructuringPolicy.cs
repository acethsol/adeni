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

        if (value is Adeni.Application.Calendar.CalendarGrant or Adeni.Application.Calendar.CalendarOAuthLaunch
            or Adeni.Application.Calendar.CalendarAuthorizationView or Adeni.Domain.Calendar.CalendarConnection
            or Adeni.Domain.Calendar.CalendarOAuthAttempt)
        {
            result = new ScalarValue("[calendar credentials redacted]");
            return true;
        }

        if (value is Adeni.Domain.Finance.BusinessGiftCard or Adeni.Domain.Finance.CommercePaymentRequest
            or Adeni.Domain.Finance.CommercePaymentAttempt or Adeni.Application.Finance.GiftCodeView
            or Adeni.Application.Finance.RedeemCommerceRequest or Adeni.Application.Finance.CommerceRequestView
            or Adeni.Application.Finance.CommerceCheckoutView)
        {
            result = new ScalarValue("[commerce credential redacted]");
            return true;
        }
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
