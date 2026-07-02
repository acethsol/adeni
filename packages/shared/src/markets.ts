export type MarketLocation = {
  lat: number;
  lng: number;
};

export type MarketConfig = {
  id: string;
  name: string;
  countryCode: string;
  currency: string;
  defaultLocation: MarketLocation;
  tagline: string;
  description: string;
  launchNote: string;
};

export const markets = {
  lagos: {
    id: "lagos",
    name: "Lagos",
    countryCode: "NG",
    currency: "NGN",
    defaultLocation: { lat: 6.5244, lng: 3.3792 },
    tagline: "Trusted local services, bookable in one place",
    description:
      "See verified businesses near you, check availability, and book with confidence — no endless messages or maybes.",
    launchNote:
      "Onboarding salons and barbers in Lagos now. Home services and more categories on the way.",
  },
} as const satisfies Record<string, MarketConfig>;

export type MarketId = keyof typeof markets;

export const DEFAULT_MARKET_ID: MarketId = "lagos";

export function getMarket(marketId: string = DEFAULT_MARKET_ID): MarketConfig {
  if (marketId in markets) {
    return markets[marketId as MarketId];
  }

  return markets[DEFAULT_MARKET_ID];
}

export function resolveMarketId(
  configuredId: string | undefined,
  fallback: MarketId = DEFAULT_MARKET_ID,
): MarketId {
  if (configuredId && configuredId in markets) {
    return configuredId as MarketId;
  }

  return fallback;
}
