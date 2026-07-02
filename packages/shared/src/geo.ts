import type { MarketConfig, MarketLocation } from "./markets";

export function parseCoordinatePair(
  value: string | undefined | null,
): MarketLocation | null {
  if (!value) {
    return null;
  }

  const [latStr, lngStr] = value.split(",");
  const lat = Number(latStr);
  const lng = Number(lngStr);

  if (!Number.isFinite(lat) || !Number.isFinite(lng)) {
    return null;
  }

  if (lat < -90 || lat > 90 || lng < -180 || lng > 180) {
    return null;
  }

  return { lat, lng };
}

export function formatCoordinatePair(location: MarketLocation): string {
  return `${location.lat},${location.lng}`;
}

/** Device location when known; otherwise the active market center. */
export function resolveSearchLocation(
  market: MarketConfig,
  coordinates?: MarketLocation | null,
): MarketLocation {
  return coordinates ?? market.defaultLocation;
}
