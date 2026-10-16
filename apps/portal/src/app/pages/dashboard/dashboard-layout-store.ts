import {
  coerceDashboardLayout,
  type DashboardLayout,
  resolveDashboardLayout,
} from "@adeni/shared";

const STORAGE_PREFIX = "adeni.portal.dashboard.layout.v1";

function storageKey(tenantId: string, practitioner: boolean): string {
  return `${STORAGE_PREFIX}.${tenantId}.${practitioner ? "practitioner" : "owner"}`;
}

export function loadDashboardLayout(tenantId: string, practitioner: boolean): DashboardLayout {
  const fallback = resolveDashboardLayout(practitioner);
  if (typeof localStorage === "undefined") {
    return fallback;
  }
  try {
    const raw = localStorage.getItem(storageKey(tenantId, practitioner));
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
