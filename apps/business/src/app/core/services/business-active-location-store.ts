/** Persisted portal branch scope; sentinel = all locations (Business reporting). */
export const PORTAL_ALL_LOCATIONS_SCOPE = "__all_locations__";

const STORAGE_PREFIX = "adeni.portal.activeLocation.v1";

function storageKey(tenantId: string): string {
  return `${STORAGE_PREFIX}.${tenantId}`;
}

function normalizePersistedScope(raw: string): string {
  if (raw === "" || raw === PORTAL_ALL_LOCATIONS_SCOPE) {
    return PORTAL_ALL_LOCATIONS_SCOPE;
  }
  return raw;
}

export function readPersistedBusinessActiveLocation(tenantId: string): string | null {
  if (typeof localStorage === "undefined" || !tenantId) {
    return null;
  }
  try {
    const raw = localStorage.getItem(storageKey(tenantId));
    if (raw === null) {
      return null;
    }
    return normalizePersistedScope(raw);
  } catch {
    return null;
  }
}

export function writePersistedBusinessActiveLocation(tenantId: string, locationId: string): void {
  if (typeof localStorage === "undefined" || !tenantId) {
    return;
  }
  try {
    localStorage.setItem(storageKey(tenantId), locationId);
  } catch {
    // Preference is optional.
  }
}

export function clearPersistedBusinessActiveLocation(tenantId: string): void {
  if (typeof localStorage === "undefined" || !tenantId) {
    return;
  }
  try {
    localStorage.removeItem(storageKey(tenantId));
  } catch {
    // Preference is optional.
  }
}
