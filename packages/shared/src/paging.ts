import { z } from "zod";

export const DEFAULT_PAGE_SIZE = 25;
export const MAX_PAGE_SIZE = 100;

/** Standard footer page-size choices for portal/admin list grids. */
export const LIST_PAGE_SIZE_OPTIONS: number[] = [10, DEFAULT_PAGE_SIZE, 50];

/** Shared list contract: `{ items, page, pageSize, totalCount }`. */
export function pagedResultSchema<T extends z.ZodTypeAny>(itemSchema: T) {
  return z
    .object({
      items: z.array(itemSchema),
      page: z.number().int().positive().optional(),
      pageSize: z.number().int().positive().optional(),
      totalCount: z.number().int().nonnegative().optional(),
    })
    .transform((data) => ({
      items: data.items,
      page: data.page ?? 1,
      pageSize: data.pageSize ?? (data.items.length > 0 ? data.items.length : DEFAULT_PAGE_SIZE),
      totalCount: data.totalCount ?? data.items.length,
    }));
}

export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
};

export function totalPages(totalCount: number, pageSize: number): number {
  return Math.max(1, Math.ceil(totalCount / pageSize));
}

export function pageRangeLabel(page: number, pageSize: number, totalCount: number): string {
  if (totalCount === 0) {
    return "0";
  }
  const start = (page - 1) * pageSize + 1;
  const end = Math.min(page * pageSize, totalCount);
  return `${start}–${end} of ${totalCount}`;
}

export function clampListPage(page: number, totalCount: number, pageSize: number): number {
  return Math.min(Math.max(1, page), totalPages(totalCount, pageSize));
}

/** Client-side list pages: slice after filter/sort. */
export function sliceListPage<T>(items: readonly T[], page: number, pageSize: number): T[] {
  if (items.length === 0) {
    return [];
  }
  const safePage = clampListPage(page, items.length, pageSize);
  const start = (safePage - 1) * pageSize;
  return items.slice(start, start + pageSize);
}
