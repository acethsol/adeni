import { getMarketById, listMarkets, type MarketConfig } from "./markets";
import { listEnabledWellnessCategorySlugs } from "./wellness-taxonomy";

/** V1 SEO city hubs — all configured markets (Lagos + Ottawa), including not-yet-live. */
export function listSeoMarkets(): MarketConfig[] {
  return listMarkets();
}

export function isSeoMarketId(marketId: string | null | undefined): boolean {
  if (!marketId?.trim()) {
    return false;
  }
  return getMarketById(marketId.trim().toLowerCase()) != null;
}

export function isSeoCategorySlug(categorySlug: string | null | undefined): boolean {
  if (!categorySlug?.trim()) {
    return false;
  }
  const slug = categorySlug.trim().toLowerCase();
  return listEnabledWellnessCategorySlugs().includes(slug);
}

export function seoMarketPath(marketId: string): string {
  return `/${marketId.trim().toLowerCase()}`;
}

export function seoCategoryPath(marketId: string, categorySlug: string): string {
  return `/${marketId.trim().toLowerCase()}/${categorySlug.trim().toLowerCase()}`;
}

/** schema.org @type hints for wellness categories (fallback LocalBusiness). */
export function localBusinessSchemaType(categorySlug: string | null | undefined): string {
  const slug = (categorySlug ?? "").trim().toLowerCase();
  const map: Record<string, string> = {
    "hair-grooming": "HairSalon",
    nails: "NailSalon",
    "spa-relaxation": "DaySpa",
    "skincare-aesthetics": "BeautySalon",
    "massage-bodywork": "HealthAndBeautyBusiness",
    fitness: "HealthClub",
    yoga: "HealthClub",
    pilates: "HealthClub",
  };
  return map[slug] ?? "LocalBusiness";
}
