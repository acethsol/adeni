import type { TenantEntitlements } from "./schemas";

/** Boolean flags on {@link TenantEntitlements} (excludes numeric limits). */
export type TenantEntitlementFlag = {
  [K in keyof TenantEntitlements]: TenantEntitlements[K] extends boolean ? K : never;
}[keyof TenantEntitlements];

export function hasTenantEntitlement(
  entitlements: TenantEntitlements | null | undefined,
  key: TenantEntitlementFlag,
): boolean {
  if (!entitlements) {
    return false;
  }
  return entitlements[key] === true;
}
