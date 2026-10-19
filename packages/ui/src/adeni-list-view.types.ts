export type AdeniListViewMode = "cards" | "table";

export type AdeniDataTableColumn<T> = {
  id: string;
  header: string;
  align?: "left" | "right";
  cell: (row: T) => string;
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
