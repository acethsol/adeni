/** Persisted portal branch scope; empty string = all locations (Business reporting). */
export const PORTAL_ALL_LOCATIONS_SCOPE = "";

const STORAGE_PREFIX = "adeni.portal.activeLocation.v1";

function storageKey(tenantId: string): string {
  return `${STORAGE_PREFIX}.${tenantId}`;
}

export function readPersistedPortalActiveLocation(tenantId: string): string | null {
  if (typeof localStorage === "undefined" || !tenantId) {
    return null;
  }
  try {
    const raw = localStorage.getItem(storageKey(tenantId));
    if (raw === null) {
      return null;
    }
    return raw;
  } catch {
    return null;
  }
}

export function writePersistedPortalActiveLocation(tenantId: string, locationId: string): void {
  if (typeof localStorage === "undefined" || !tenantId) {
    return;
  }
  try {
    localStorage.setItem(storageKey(tenantId), locationId);
  } catch {
    // Preference is optional.
  }
}

export function clearPersistedPortalActiveLocation(tenantId: string): void {
  if (typeof localStorage === "undefined" || !tenantId) {
    return;
  }
  try {
    localStorage.removeItem(storageKey(tenantId));
  } catch {
    // Preference is optional.
  }
}
