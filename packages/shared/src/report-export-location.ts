import { hasTenantEntitlement } from "./entitlements";
import type { BusinessLocation, TenantEntitlements } from "./schemas";

export function activeBusinessLocations(
  locations: readonly BusinessLocation[] | null | undefined,
): BusinessLocation[] {
  if (!locations?.length) {
    return [];
  }
  return locations.filter((location) => location.isActive);
}

export function hasMultiLocationReportingEntitlement(
  entitlements: TenantEntitlements | null | undefined,
): boolean {
  return hasTenantEntitlement(entitlements, "multiLocationReporting");
}

/** True when the tenant must scope exports to one branch (API multi-location gate). */
export function needsReportExportLocationPicker(
  locations: readonly BusinessLocation[] | null | undefined,
  entitlements: TenantEntitlements | null | undefined,
): boolean {
  const active = activeBusinessLocations(locations);
  return active.length > 1 && !hasMultiLocationReportingEntitlement(entitlements);
}

function defaultBranchLocationId(active: BusinessLocation[]): string | undefined {
  if (active.length === 0) {
    return undefined;
  }
  const primary = active.find((location) => location.isPrimary);
  return (primary ?? active[0])!.id;
}

/** Resolves `locationId` for tenant report export API calls. */
export function resolveReportExportLocationId(options: {
  locations: readonly BusinessLocation[] | null | undefined;
  entitlements: TenantEntitlements | null | undefined;
  selectedLocationId?: string | null;
}): string | undefined {
  const active = activeBusinessLocations(options.locations);
  if (active.length <= 1) {
    return active[0]?.id;
  }
  if (hasMultiLocationReportingEntitlement(options.entitlements)) {
    const selected = options.selectedLocationId?.trim();
    return selected || undefined;
  }
  const selected = options.selectedLocationId?.trim();
  if (selected && active.some((location) => location.id === selected)) {
    return selected;
  }
  return defaultBranchLocationId(active);
}

/** Same resolution as report exports — optional `locationId` for tenant list/dashboard APIs. */
export const resolveTenantApiLocationId = resolveReportExportLocationId;
