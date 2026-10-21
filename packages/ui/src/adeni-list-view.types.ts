export type AdeniListViewMode = "cards" | "table";

export type AdeniDataTableSortDirection = "asc" | "desc";

export type AdeniDataTableColumn<T> = {
  id: string;
  header: string;
  align?: "left" | "right";
  cell: (row: T) => string;
  /** Sortable when omitted or true; set false for non-data columns (e.g. composite actions). */
  sortable?: boolean;
  sortValue?: (row: T) => string | number;
};

const STORAGE_PREFIX = "adeni.listView.";

export function readPersistedListViewMode(
  storageKey: string,
  fallback: AdeniListViewMode = "cards",
): AdeniListViewMode {
  if (typeof globalThis.localStorage === "undefined") {
    return fallback;
  }
  const raw = globalThis.localStorage.getItem(`${STORAGE_PREFIX}${storageKey}`);
  return raw === "table" || raw === "cards" ? raw : fallback;
}

export function persistListViewMode(storageKey: string, mode: AdeniListViewMode): void {
  if (typeof globalThis.localStorage === "undefined") {
    return;
  }
  globalThis.localStorage.setItem(`${STORAGE_PREFIX}${storageKey}`, mode);
}
