import type { AdeniDataTableColumn, AdeniDataTableSortDirection } from "./adeni-list-view.types";

export function isAdeniDataTableColumnSortable<T>(
  column: AdeniDataTableColumn<T>,
): boolean {
  return column.sortable !== false;
}

function sortValueForRow<T>(column: AdeniDataTableColumn<T>, row: T): string | number {
  if (column.sortValue) {
    return column.sortValue(row);
  }
  return column.cell(row);
}

function compareSortValues(
  a: string | number,
  b: string | number,
  direction: AdeniDataTableSortDirection,
): number {
  const factor = direction === "asc" ? 1 : -1;
  if (typeof a === "number" && typeof b === "number") {
    return (a - b) * factor;
  }
  return (
    String(a).localeCompare(String(b), undefined, { numeric: true, sensitivity: "base" }) *
    factor
  );
}

/** Client-side sort for list grids; use the same helper for card + table parity. */
export function sortDataRows<T>(
  rows: readonly T[],
  sortColumnId: string | null,
  sortDirection: AdeniDataTableSortDirection,
  columns: readonly AdeniDataTableColumn<T>[],
): T[] {
  if (!sortColumnId || rows.length < 2) {
    return [...rows];
  }
  const column = columns.find((item) => item.id === sortColumnId);
  if (!column || !isAdeniDataTableColumnSortable(column)) {
    return [...rows];
  }
  return [...rows].sort((left, right) =>
    compareSortValues(
      sortValueForRow(column, left),
      sortValueForRow(column, right),
      sortDirection,
    ),
  );
}
