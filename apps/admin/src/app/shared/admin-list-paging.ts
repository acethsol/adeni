/** Admin ops grids: small pages keep action-heavy rows scannable. */
export const ADMIN_LIST_PAGE_SIZE = 10;

export function adminListTotalPages(totalCount: number, pageSize: number): number {
  return Math.max(1, Math.ceil(totalCount / pageSize));
}

export function adminListRangeLabel(page: number, pageSize: number, totalCount: number): string {
  if (totalCount === 0) {
    return "0 results";
  }
  const start = (page - 1) * pageSize + 1;
  const end = Math.min(page * pageSize, totalCount);
  const pages = adminListTotalPages(totalCount, pageSize);
  if (pages <= 1) {
    return `${start}–${end} of ${totalCount}`;
  }
  return `${start}–${end} of ${totalCount} · page ${page} of ${pages}`;
}
