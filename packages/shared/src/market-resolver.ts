import {
  getMarketById,
  listLiveMarkets,
  listMarkets,
  type MarketConfig,
  type MarketLocation,
} from "./markets";

export type MarketResolutionInput = {
  /** Dev override, cookie, or explicit user selection. */
  marketId?: string | null;
  /** Browser / device location when available. */
  coordinates?: MarketLocation | null;
  /** Max distance (km) to match a market from coordinates. */
  maxDistanceKm?: number;
};

export type ResolvedMarket = {
  market: MarketConfig;
  source: "explicit" | "geo" | "fallback";
};

const DEFAULT_MAX_DISTANCE_KM = 250;

export function resolveMarket(input: MarketResolutionInput = {}): ResolvedMarket {
  const maxDistanceKm = input.maxDistanceKm ?? DEFAULT_MAX_DISTANCE_KM;

  if (input.marketId) {
    const explicit = getMarketById(input.marketId);
    if (explicit) {
      return { market: explicit, source: "explicit" };
    }
  }

  if (input.coordinates) {
    const geoMatch = findNearestMarket(input.coordinates, maxDistanceKm);
    if (geoMatch) {
      return { market: geoMatch, source: "geo" };
    }
  }

  const fallback = getDefaultMarket();
  return { market: fallback, source: "fallback" };
}

/** Default when no explicit selection or geo match — prefers GTM live market, else first catalog entry. */
export function getDefaultMarket(): MarketConfig {
  const live = listLiveMarkets()[0];
  if (live) {
    return live;
  }

  const all = listMarkets();
  if (all.length === 0) {
    throw new Error("No markets configured in the market catalog.");
  }

  return all[0];
}

/** Nearest market from the full catalog within range. */
export function findNearestMarket(
  coordinates: MarketLocation,
  maxDistanceKm: number,
): MarketConfig | null {
  let best: MarketConfig | null = null;
  let bestDistance = Number.POSITIVE_INFINITY;

  for (const market of listMarkets()) {
    const distance = distanceKm(coordinates, market.defaultLocation);
    if (distance < bestDistance) {
      best = market;
      bestDistance = distance;
    }
  }

  if (best === null || bestDistance > maxDistanceKm) {
    return null;
  }

  return best;
}

/** Nearest GTM-live market within range (for supply-focused flows). */
export function findNearestLiveMarket(
  coordinates: MarketLocation,
  maxDistanceKm: number,
): MarketConfig | null {
  let best: MarketConfig | null = null;
  let bestDistance = Number.POSITIVE_INFINITY;

  for (const market of listLiveMarkets()) {
    const distance = distanceKm(coordinates, market.defaultLocation);
    if (distance < bestDistance) {
      best = market;
      bestDistance = distance;
    }
  }

  if (best === null || bestDistance > maxDistanceKm) {
    return null;
  }

  return best;
}

function distanceKm(a: MarketLocation, b: MarketLocation): number {
  const earthRadiusKm = 6371;
  const dLat = toRadians(b.lat - a.lat);
  const dLng = toRadians(b.lng - a.lng);
  const lat1 = toRadians(a.lat);
  const lat2 = toRadians(b.lat);

  const sinLat = Math.sin(dLat / 2);
  const sinLng = Math.sin(dLng / 2);
  const h =
    sinLat * sinLat + Math.cos(lat1) * Math.cos(lat2) * sinLng * sinLng;

  return 2 * earthRadiusKm * Math.asin(Math.min(1, Math.sqrt(h)));
}

function toRadians(degrees: number): number {
  return (degrees * Math.PI) / 180;
}
