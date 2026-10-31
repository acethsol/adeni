namespace Adeni.Infrastructure.Finance;

using Adeni.Application.Finance;
using Adeni.Domain.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public sealed class PaymentCallbackUrlValidator(
    IOptions<FinanceOptions> financeOptions,
    IHostEnvironment environment) : IPaymentCallbackUrlValidator
{
    private FinanceOptions Options => financeOptions.Value;

    public Result ValidateOptionalCallbackUrl(string? callbackUrl)
    {
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            return Result.Success();
        }

        if (!Uri.TryCreate(callbackUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return Result.Failure(Error.Validation("Callback URL is not valid."));
        }

        if (!string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return Result.Failure(Error.Validation("Callback URL must not include user info or fragments."));
        }

        var isDev = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        if (!isDev && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Validation("Callback URL must use HTTPS."));
        }

        if (!IsAllowedOrigin(uri, isDev))
        {
            return Result.Failure(Error.Validation("Callback URL origin is not allowed."));
        }

        return Result.Success();
    }

    public string BuildCustomerBookingCallbackUrl(Guid paymentIntentId)
    {
        var baseUrl = Options.ReceiptBaseUrl.TrimEnd('/');
        return $"{baseUrl}?id={paymentIntentId}";
    }

    private bool IsAllowedOrigin(Uri uri, bool isDev)
    {
        if (isDev && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                      || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var configured = Options.AllowedCallbackOrigins;
        if (configured is { Count: > 0 })
        {
            var origin = $"{uri.Scheme}://{uri.Authority}".TrimEnd('/');
            return configured.Any(allowed =>
                string.Equals(allowed.Trim().TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }
}
