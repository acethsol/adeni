namespace Adeni.Application.Finance;

using Adeni.Domain.Common;

public interface IPaymentCallbackUrlValidator
{
    Result ValidateOptionalCallbackUrl(string? callbackUrl);

    string BuildCustomerBookingCallbackUrl(Guid paymentIntentId);
}
