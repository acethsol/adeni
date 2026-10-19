namespace Adeni.Application.Finance;

/// <summary>Resolves licensed commerce/billing providers by tenant market or country.</summary>
public interface IPaymentProviderRegistry
{
    Task<string> ResolveCommerceProviderKeyForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    string ResolveCommerceProviderKeyForCountry(string? countryCode);

    string ResolveCommerceProviderKeyForMarket(string? marketId);

    Task<string> ResolveBillingProviderKeyForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    string ResolveBillingProviderKeyForCountry(string? countryCode);

    IPaymentProvider GetCommerceProvider(string providerKey);
}
