namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Common;
using Adeni.Domain.Subscriptions;

public interface ISubscriptionService
{
    Task<Result<SubscriptionUsageResponse>> GetTenantUsageAsync(
        Guid tenantId,
        string auth0Sub,
        Guid? locationId = null,
        CancellationToken cancellationToken = default);
}
