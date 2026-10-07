import catalog from "./data/wellness-categories.json";

type WellnessCatalog = {
  marketFeaturedOrder: Record<string, string[]>;
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
