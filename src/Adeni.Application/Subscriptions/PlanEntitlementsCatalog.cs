namespace Adeni.Application.Subscriptions;

using Adeni.Domain.Subscriptions;

/// <summary>
/// Plan capability matrix facade. Values load from packages/shared/src/data/plan-entitlements.json
/// via <see cref="IPlanEntitlementsMatrix"/> (registered at startup in Infrastructure).
/// </summary>
public static class PlanEntitlementsCatalog
{
    private static IPlanEntitlementsMatrix? Matrix { get; set; }

    public static void Initialize(IPlanEntitlementsMatrix matrix) =>
        Matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));

    public static TenantEntitlements ForSku(SubscriptionSku sku) =>
        (Matrix ?? throw new InvalidOperationException(
            "Plan entitlements matrix is not initialized. Register IPlanEntitlementsMatrix at startup."))
        .ForSku(sku);
}
