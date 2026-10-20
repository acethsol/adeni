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

export function canAddLocationAtCount(
  entitlements: TenantEntitlements | null | undefined,
  activeLocationCount: number,
): boolean {
  if (!entitlements) {
    return false;
  }
  if (entitlements.maxLocations == null) {
    return true;
  }
  return activeLocationCount < entitlements.maxLocations;
}

export function canAddPractitionerAtCount(
  entitlements: TenantEntitlements | null | undefined,
  activePractitionerCount: number,
): boolean {
  if (!entitlements) {
    return false;
  }
  if (entitlements.maxPractitioners == null) {
    return true;
  }
  return activePractitionerCount < entitlements.maxPractitioners;
}
