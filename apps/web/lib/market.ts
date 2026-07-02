import { cookies, headers } from "next/headers";
import {
  parseCoordinatePair,
  resolveMarket,
  resolveSearchLocation,
  type MarketConfig,
  type MarketLocation,
  type ResolvedMarket,
} from "@adeni/shared";

export const MARKET_COOKIE_NAME = "adeni_market";
export const COORDS_COOKIE_NAME = "adeni_coords";

export function resolveMarketFromEnv(): string | undefined {
  return process.env.NEXT_PUBLIC_ADENI_MARKET ?? process.env.ADENI_MARKET;
}

export async function getStoredCoordinates(): Promise<MarketLocation | null> {
  const cookieStore = await cookies();
  return parseCoordinatePair(cookieStore.get(COORDS_COOKIE_NAME)?.value);
}

export async function getActiveMarket(): Promise<ResolvedMarket> {
  const cookieStore = await cookies();
  const headerStore = await headers();

  const cookieMarket = cookieStore.get(MARKET_COOKIE_NAME)?.value;
  const headerMarket = headerStore.get("x-adeni-market") ?? undefined;
  const envMarket = resolveMarketFromEnv();
  const coordinates = await getStoredCoordinates();

  return resolveMarket({
    marketId: cookieMarket ?? headerMarket ?? envMarket,
    coordinates,
  });
}

export async function getActiveMarketConfig(): Promise<MarketConfig> {
  return (await getActiveMarket()).market;
}

export async function getDiscoveryLocation(): Promise<MarketLocation> {
  const market = await getActiveMarketConfig();
  const coordinates = await getStoredCoordinates();
  return resolveSearchLocation(market, coordinates);
}
