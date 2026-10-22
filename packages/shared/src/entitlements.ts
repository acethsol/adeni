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
  const value = entitlements[key as keyof TenantEntitlements];
  return value === true;
}

/** CSV/register exports — all paid plans; API is authoritative when absent client-side. */
export function hasBasicExportsEntitlement(
  entitlements: TenantEntitlements | null | undefined,
): boolean {
  if (!entitlements) {
    return true;
  }
  return entitlements.basicExports !== false;
}

/** Excel exports — Pro/Business only; strict match to API gates. */
export function hasExcelExportEntitlement(
  entitlements: TenantEntitlements | null | undefined,
): boolean {
  if (!entitlements) {
    return false;
  }
  return entitlements.excelExport === true;
}

export function resolveTenantEntitlements(
  ...sources: Array<TenantEntitlements | null | undefined>
): TenantEntitlements | null {
  for (const source of sources) {
    if (source) {
      return source;
    }
  }
  return null;
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
