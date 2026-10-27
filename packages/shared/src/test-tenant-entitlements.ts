import type { SubscriptionSku } from "./country-pricing";
import { PLAN_ENTITLEMENTS, planAllowsMultipleLocations } from "./plan-entitlements";
import type { TenantEntitlements } from "./schemas";

/** Test fixture: plan seed shaped like the API `TenantEntitlements` contract. */
export function tenantEntitlementsFromPlan(sku: SubscriptionSku): TenantEntitlements {
  const seed = PLAN_ENTITLEMENTS[sku];
  return { ...seed, multiLocation: planAllowsMultipleLocations(seed) };
}
