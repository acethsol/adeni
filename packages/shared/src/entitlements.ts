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

/** Sales-summary PDF and compensation exports — Pro/Business; matches API `AdvancedReports` gate. */
export function hasAdvancedReportsEntitlement(
  entitlements: TenantEntitlements | null | undefined,
): boolean {
  if (!entitlements) {
    return false;
  }
  return entitlements.advancedReports === true;
}

/** Merge entitlement flags from API profile, usage, and `/tenant/entitlements` (OR for tier gates). */
export function mergeTenantEntitlements(
  ...sources: Array<TenantEntitlements | null | undefined>
): TenantEntitlements | null {
  const present = sources.filter((source): source is TenantEntitlements => source != null);
  if (present.length === 0) {
    return null;
  }
  if (present.length === 1) {
    return present[0]!;
  }

  const merged = { ...present[0]! };
  for (const source of present.slice(1)) {
    Object.assign(merged, source);
  }

  merged.basicExports = !present.every((source) => source.basicExports === false);
  merged.excelExport = present.some((source) => source.excelExport === true);
  merged.advancedReports = present.some((source) => source.advancedReports === true);
  merged.multiLocationReporting = present.some(
    (source) => source.multiLocationReporting === true,
  );

  return merged;
}

/** @deprecated Prefer {@link mergeTenantEntitlements} — same behavior (merged, not first-wins). */
export function resolveTenantEntitlements(
  ...sources: Array<TenantEntitlements | null | undefined>
): TenantEntitlements | null {
  return mergeTenantEntitlements(...sources);
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
