export type ListPaginationWindowItem =
  | { kind: "page"; page: number }
  | { kind: "gap" };

/** Compact page list with ellipses (1 … 4 5 6 … 20). */
export function buildListPaginationWindow(
  current: number,
  total: number,
  siblingRadius = 1,
): ListPaginationWindowItem[] {
  if (total <= 1) {
    return [];
  }
  if (total <= 7) {
    return Array.from({ length: total }, (_, index) => ({
      kind: "page" as const,
      page: index + 1,
    }));
  }

  const pages = new Set<number>([1, total]);
  for (let page = current - siblingRadius; page <= current + siblingRadius; page += 1) {
    if (page >= 1 && page <= total) {
      pages.add(page);
    }
  }

  const sorted = [...pages].sort((a, b) => a - b);
  const items: ListPaginationWindowItem[] = [];
  for (let index = 0; index < sorted.length; index += 1) {
    const page = sorted[index]!;
    if (index > 0 && page - sorted[index - 1]! > 1) {
      items.push({ kind: "gap" });
    }
    items.push({ kind: "page", page });
  }
  return items;
}
