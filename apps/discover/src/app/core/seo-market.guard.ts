import { CanMatchFn } from "@angular/router";
import {
  isSeoCategorySlug,
  isSeoMarketId,
  isSeoNeighborhoodSlug,
} from "@adeni/shared";

/**
 * Matches:
 * - /:marketId
 * - /:marketId/:categorySlug | /:marketId/:areaSlug
 * - /:marketId/:areaSlug/:categorySlug
 */
export const seoMarketCanMatch: CanMatchFn = (_route, segments) => {
  const marketId = segments[0]?.path ?? "";
  if (!isSeoMarketId(marketId)) {
    return false;
  }
  if (segments.length === 1) {
    return true;
  }
  if (segments.length === 2) {
    const segment = segments[1]?.path ?? "";
    return isSeoCategorySlug(segment) || isSeoNeighborhoodSlug(marketId, segment);
  }
  if (segments.length === 3) {
    const area = segments[1]?.path ?? "";
    const category = segments[2]?.path ?? "";
    return isSeoNeighborhoodSlug(marketId, area) && isSeoCategorySlug(category);
  }
  return false;
};
