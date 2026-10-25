namespace Adeni.Application.Messaging;

using Adeni.Application.Tenancy;

public interface ICustomerCommunicationActivityQuery
{
    Task<CustomerActivityBatch> GetActivityAsync(CustomerProfileScope scope, DateTimeOffset from, DateTimeOffset asOf, CancellationToken cancellationToken);
}
