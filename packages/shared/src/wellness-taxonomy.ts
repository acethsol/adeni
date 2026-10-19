import catalog from "./data/wellness-categories.json";

type WellnessCategoryRow = {
  slug: string;
  enabled?: boolean;
  parentSlug?: string;
  wellnessScope?: boolean;
};

type WellnessCatalog = {
  marketFeaturedOrder: Record<string, string[]>;
  categories: WellnessCategoryRow[];
};

const wellnessCatalog = catalog as WellnessCatalog;

/** Featured Explore category slugs for a market (from wellness-categories.json). */
export function getMarketFeaturedCategorySlugs(
  marketId?: string | null,
  limit = 6,
): string[] {
  const order = wellnessCatalog.marketFeaturedOrder;
  const key = (marketId ?? "").trim().toLowerCase();
  const list = (key && order[key] ? order[key] : order["default"]) ?? [];
  return list.slice(0, Math.max(0, limit));
}

/** Enabled beauty/wellness category slugs (SEO landings + sitemaps). */
export function listEnabledWellnessCategorySlugs(): string[] {
  return wellnessCatalog.categories
    .filter(
      (c) =>
        c.enabled !== false
        && c.parentSlug === "beauty-wellness"
        && c.wellnessScope !== false,
    )
    .map((c) => c.slug);
}
