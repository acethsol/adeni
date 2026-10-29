import {
  coerceDashboardLayout,
  type DashboardLayout,
  resolveDashboardLayout,
} from "@adeni/shared";

/** Persists customized layouts per tenant/role. Width defaults in @adeni/shared only apply on first load or after reset — not for saved layouts (clear storage or re-apply a preset to pick up new defaults). */
const STORAGE_PREFIX = "adeni.businessApp.dashboard.layout.v1";
const LEGACY_STORAGE_PREFIX = "adeni.portal.dashboard.layout.v1";

function storageKey(tenantId: string, practitioner: boolean): string {
  return `${STORAGE_PREFIX}.${tenantId}.${practitioner ? "practitioner" : "owner"}`;
}

export function loadDashboardLayout(tenantId: string, practitioner: boolean): DashboardLayout {
  const fallback = resolveDashboardLayout(practitioner);
  if (typeof localStorage === "undefined") {
    return fallback;
  }
  try {
    const key = storageKey(tenantId, practitioner);
    let raw = localStorage.getItem(key);
    if (!raw) {
      const legacyKey = `${LEGACY_STORAGE_PREFIX}.${tenantId}.${practitioner ? "practitioner" : "owner"}`;
      raw = localStorage.getItem(legacyKey);
      if (raw) {
        localStorage.setItem(key, raw);
      }
    }
    if (!raw) {
      return fallback;
    }
    return coerceDashboardLayout(JSON.parse(raw) as unknown, fallback);
  } catch {
    return fallback;
  }
}

export function saveDashboardLayout(
  tenantId: string,
  practitioner: boolean,
  layout: DashboardLayout,
): void {
  if (typeof localStorage === "undefined") {
    return;
  }
  localStorage.setItem(storageKey(tenantId, practitioner), JSON.stringify(layout));
}

export function clearDashboardLayout(tenantId: string, practitioner: boolean): void {
  if (typeof localStorage === "undefined") {
    return;
  }
  localStorage.removeItem(storageKey(tenantId, practitioner));
}
