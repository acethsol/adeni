namespace Adeni.Application.Finance;

using Adeni.Application.Tenancy;

/// <summary>Amounts recorded on completed linked payment intents, excluding refunds and external tills. No inferred lifetime spend.</summary>
public interface ICustomerCommerceQuery
{
    Task<IReadOnlyList<CustomerCurrencyTotal>> GetTotalsAsync(CustomerProfileScope scope, DateTimeOffset asOf, CancellationToken cancellationToken);
    Task<CustomerActivityBatch> GetActivityAsync(CustomerProfileScope scope, DateTimeOffset from, DateTimeOffset asOf, CancellationToken cancellationToken);
}
