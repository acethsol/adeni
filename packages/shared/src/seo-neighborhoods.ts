/** Curated V1 neighborhood hubs for Discover SEO (Ottawa + Lagos). */
export type SeoNeighborhood = {
  /** URL segment under /{marketId}/… */
  slug: string;
  /** Display label */
  label: string;
  marketId: string;
  /** Discovery `q` phrase (matches location.Area). */
  query: string;
};

/**
 * Primary SEO neighborhoods. Aliases (e.g. "vi") live in search-intent only —
 * they do not get their own hub URLs.
 */
export const SEO_NEIGHBORHOODS: readonly SeoNeighborhood[] = [
  { slug: "lekki", label: "Lekki", marketId: "lagos", query: "lekki" },
  { slug: "ikeja", label: "Ikeja", marketId: "lagos", query: "ikeja" },
  {
    slug: "victoria-island",
    label: "Victoria Island",
    marketId: "lagos",
    query: "victoria island",
  },
  { slug: "yaba", label: "Yaba", marketId: "lagos", query: "yaba" },
  { slug: "surulere", label: "Surulere", marketId: "lagos", query: "surulere" },
  { slug: "ajah", label: "Ajah", marketId: "lagos", query: "ajah" },
  { slug: "ikoyi", label: "Ikoyi", marketId: "lagos", query: "ikoyi" },
  { slug: "centretown", label: "Centretown", marketId: "ottawa", query: "centretown" },
  { slug: "glebe", label: "The Glebe", marketId: "ottawa", query: "glebe" },
  { slug: "kanata", label: "Kanata", marketId: "ottawa", query: "kanata" },
  { slug: "westboro", label: "Westboro", marketId: "ottawa", query: "westboro" },
  { slug: "orleans", label: "Orléans", marketId: "ottawa", query: "orleans" },
] as const;

const byMarket = new Map<string, SeoNeighborhood[]>();
const bySlug = new Map<string, SeoNeighborhood>();

for (const n of SEO_NEIGHBORHOODS) {
  const list = byMarket.get(n.marketId) ?? [];
  list.push(n);
  byMarket.set(n.marketId, list);
  bySlug.set(`${n.marketId}:${n.slug}`, n);
}

export function listSeoNeighborhoods(marketId?: string | null): SeoNeighborhood[] {
  if (!marketId?.trim()) {
    return [...SEO_NEIGHBORHOODS];
  }
  return byMarket.get(marketId.trim().toLowerCase()) ?? [];
}

export function getSeoNeighborhood(
  marketId: string | null | undefined,
  areaSlug: string | null | undefined,
): SeoNeighborhood | null {
  if (!marketId?.trim() || !areaSlug?.trim()) {
    return null;
  }
  return bySlug.get(`${marketId.trim().toLowerCase()}:${areaSlug.trim().toLowerCase()}`) ?? null;
}

export function isSeoNeighborhoodSlug(
  marketId: string | null | undefined,
  areaSlug: string | null | undefined,
): boolean {
  return getSeoNeighborhood(marketId, areaSlug) != null;
}

export function seoNeighborhoodPath(marketId: string, areaSlug: string): string {
  return `/${marketId.trim().toLowerCase()}/${areaSlug.trim().toLowerCase()}`;
}

export function seoNeighborhoodCategoryPath(
  marketId: string,
  areaSlug: string,
  categorySlug: string,
): string {
  return `${seoNeighborhoodPath(marketId, areaSlug)}/${categorySlug.trim().toLowerCase()}`;
}
