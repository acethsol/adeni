export type ListSortDirection = "asc" | "desc";

export type ListSortParams = {
  sortBy?: string;
  sortDir?: ListSortDirection;
};

/** Append optional server sort query params (sortBy + sortDir). */
export function appendListSortParams(
  query: URLSearchParams,
  sort?: ListSortParams | null,
): void {
  if (!sort?.sortBy) {
    return;
  }

  query.set("sortBy", sort.sortBy);
  if (sort.sortDir) {
    query.set("sortDir", sort.sortDir);
  }
}

export function listSortParamsFromColumn(
  sortColumnId: string | null,
  sortDirection: ListSortDirection,
): ListSortParams | undefined {
  if (!sortColumnId) {
    return undefined;
  }

  return { sortBy: sortColumnId, sortDir: sortDirection };
}
