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
  /** Platform copy — no city names. */
  tagline: string;
  description: string;
  /** Optional GTM banner shown only when this market is active. */
  launchNote?: string;
  /** Supply / GTM status — does not restrict market access in the app. */
  isLive: boolean;
};

/**
 * Market catalog — data, not platform logic.
 * GTM launches by flipping `isLive` when supply is ready; all markets remain browsable.
 */
export const markets = {
  lagos: {
    id: "lagos",
    name: "Lagos",
    countryCode: "NG",
    currency: "NGN",
    timeZoneId: "Africa/Lagos",
    defaultLocation: { lat: 6.5244, lng: 3.3792 },
    tagline: "Trusted local services, bookable in one place",
    description:
      "See verified businesses near you, check availability, and book with confidence — no endless messages or maybes.",
    isLive: true,
  },
  abuja: {
    id: "abuja",
    name: "Abuja",
    countryCode: "NG",
    currency: "NGN",
    timeZoneId: "Africa/Lagos",
    defaultLocation: { lat: 9.0765, lng: 7.3986 },
    tagline: "Trusted local services, bookable in one place",
    description:
      "See verified businesses near you, check availability, and book with confidence — no endless messages or maybes.",
    isLive: false,
  },
  ottawa: {
    id: "ottawa",
    name: "Ottawa",
    countryCode: "CA",
    currency: "CAD",
    timeZoneId: "America/Toronto",
    defaultLocation: { lat: 45.4215, lng: -75.6972 },
    tagline: "Trusted local services, bookable in one place",
    description:
      "See verified businesses near you, check availability, and book with confidence — no endless messages or maybes.",
    isLive: false,
  },
  toronto: {
    id: "toronto",
    name: "Toronto",
    countryCode: "CA",
    currency: "CAD",
    timeZoneId: "America/Toronto",
    defaultLocation: { lat: 43.6532, lng: -79.3832 },
    tagline: "Trusted local services, bookable in one place",
    description:
      "See verified businesses near you, check availability, and book with confidence — no endless messages or maybes.",
    isLive: false,
  },
  houston: {
    id: "houston",
    name: "Houston",
    countryCode: "US",
    currency: "USD",
    timeZoneId: "America/Chicago",
    defaultLocation: { lat: 29.7604, lng: -95.3698 },
    tagline: "Trusted local services, bookable in one place",
    description:
      "See verified businesses near you, check availability, and book with confidence — no endless messages or maybes.",
    isLive: false,
  },
  dallas: {
    id: "dallas",
    name: "Dallas",
    countryCode: "US",
    currency: "USD",
    timeZoneId: "America/Chicago",
    defaultLocation: { lat: 32.7767, lng: -96.797 },
    tagline: "Trusted local services, bookable in one place",
    description:
      "See verified businesses near you, check availability, and book with confidence — no endless messages or maybes.",
    isLive: false,
  },
} as const satisfies Record<string, MarketConfig>;

export type MarketId = keyof typeof markets;

export function listMarkets(): MarketConfig[] {
  return Object.values(markets);
}

export function listLiveMarkets(): MarketConfig[] {
  return listMarkets().filter((market) => market.isLive);
}

export function getMarketById(marketId: string): MarketConfig | null {
  if (marketId in markets) {
    return markets[marketId as MarketId];
  }

  return null;
}
