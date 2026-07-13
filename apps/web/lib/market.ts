import { cookies, headers } from "next/headers";
import {
  parseCoordinatePair,
  resolveMarket,
  resolveSearchLocation,
  type MarketConfig,
  type MarketLocation,
  type ResolvedMarket,
} from "@adeni/shared";
import { COORDS_COOKIE_NAME, MARKET_COOKIE_NAME } from "@/lib/market-constants";
import { getMarkets } from "@/lib/markets-api";

export function resolveMarketFromEnv(): string | undefined {
  return process.env.NEXT_PUBLIC_ADENI_MARKET ?? process.env.ADENI_MARKET;
}

export async function getStoredCoordinates(): Promise<MarketLocation | null> {
  const cookieStore = await cookies();
  return parseCoordinatePair(cookieStore.get(COORDS_COOKIE_NAME)?.value);
}

export async function getActiveMarket(): Promise<ResolvedMarket> {
  const [catalog, cookieStore, headerStore] = await Promise.all([
    getMarkets(),
    cookies(),
    headers(),
  ]);

  const explicitMarket = cookieStore.get(MARKET_COOKIE_NAME)?.value
    ?? headerStore.get("x-adeni-market")
    ?? undefined;
  const coordinates = parseCoordinatePair(cookieStore.get(COORDS_COOKIE_NAME)?.value);

  // URL/cookie override (?market=) — intentional user selection.
  if (explicitMarket) {
    return resolveMarket({ marketId: explicitMarket, coordinates }, catalog);
  }

  // Device location beats dev env default when coordinates are available.
  if (coordinates) {
    const geoResolved = resolveMarket({ coordinates }, catalog);
    if (geoResolved.source === "geo") {
      return geoResolved;
    }
  }

  const envMarket = resolveMarketFromEnv();
  if (envMarket) {
    return resolveMarket({ marketId: envMarket, coordinates }, catalog);
  }

  return resolveMarket({ coordinates }, catalog);
}

export async function getActiveMarketConfig(): Promise<MarketConfig> {
  return (await getActiveMarket()).market;
}

export async function getDiscoveryLocation(): Promise<MarketLocation> {
  const market = await getActiveMarketConfig();
  const coordinates = await getStoredCoordinates();
  return resolveSearchLocation(market, coordinates);
}
