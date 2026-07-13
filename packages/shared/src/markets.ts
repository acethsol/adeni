import type { LocaleId } from "./i18n/types";
import catalog from "./data/markets.json";
import type { MarketApiItem } from "./schemas";

export type MarketLocation = {
  lat: number;
  lng: number;
};

export type MarketConfig = {
  id: string;
  name: string;
  countryCode: string;
  currency: string;
  timeZoneId: string;
  defaultLocation: MarketLocation;
  languages: LocaleId[];
  launchNote?: string;
  isLive: boolean;
};

type MarketCatalogFile = {
  markets: MarketConfig[];
};

const parsed = catalog as MarketCatalogFile;

const marketMap = new Map<string, MarketConfig>(
  parsed.markets.map((market) => [market.id, market]),
);

export type MarketId = string;

export function mapApiMarketToConfig(market: MarketApiItem): MarketConfig {
  return {
    id: market.id,
    name: market.name,
    countryCode: market.countryCode,
    currency: market.currency,
    timeZoneId: market.timeZoneId,
    defaultLocation: market.defaultLocation,
    languages: market.languages as LocaleId[],
    launchNote: market.launchNote ?? undefined,
    isLive: market.isLive,
  };
}

export function listMarkets(): MarketConfig[] {
  return parsed.markets;
}

export function listLiveMarkets(): MarketConfig[] {
  return parsed.markets.filter((market) => market.isLive);
}

export function getMarketById(marketId: string): MarketConfig | null {
  return marketMap.get(marketId) ?? null;
}

export function getMarketByIdFromCatalog(
  marketId: string,
  catalogMarkets: MarketConfig[],
): MarketConfig | null {
  return catalogMarkets.find((market) => market.id === marketId) ?? null;
}

export function getMarketCatalogVersion(): string {
  return parsed.markets.map((market) => market.id).join(",");
}
