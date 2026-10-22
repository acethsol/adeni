namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Subscriptions;

public interface IPlanEntitlementsMatrix
{
    TenantEntitlements ForSku(SubscriptionSku sku);
}
