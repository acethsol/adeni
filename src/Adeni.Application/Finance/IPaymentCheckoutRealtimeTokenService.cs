namespace Adeni.Application.Finance;

public sealed record PaymentCheckoutRealtimeClaims(
    Guid PaymentIntentId,
    string ProviderReference);

public interface IPaymentCheckoutRealtimeTokenService
{
    string Issue(Guid paymentIntentId, string providerReference, TimeSpan? ttl = null);

    bool TryResolve(string token, out PaymentCheckoutRealtimeClaims? claims);
}
