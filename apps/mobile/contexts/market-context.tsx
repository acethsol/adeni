import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";
import * as Location from "expo-location";
import {
  resolveMarket,
  resolveSearchLocation,
  listMarkets,
  type MarketConfig,
  type MarketLocation,
  type ResolvedMarket,
} from "@adeni/shared";
import { createPublicApiClient } from "@/lib/api";
import { readStoredMarketId, writeStoredMarketId } from "@/lib/locale-storage";

export type ActiveMarketState = {
  market: MarketConfig;
  source: ResolvedMarket["source"];
  coordinates: MarketLocation | null;
  searchLocation: MarketLocation;
  loading: boolean;
  locationDenied: boolean;
  setMarketId: (marketId: string) => Promise<void>;
};

const envMarketId = process.env.EXPO_PUBLIC_ADENI_MARKET?.trim() || undefined;

function resolveFromInputs(
  coordinates: MarketLocation | null,
  explicitMarketId: string | undefined,
  catalog: MarketConfig[],
): Pick<ActiveMarketState, "market" | "source" | "coordinates" | "searchLocation"> {
  if (explicitMarketId) {
    const resolved = resolveMarket({ marketId: explicitMarketId, coordinates }, catalog);
    return {
      market: resolved.market,
      source: resolved.source,
      coordinates,
      searchLocation: resolveSearchLocation(resolved.market, coordinates),
    };
  }

  if (coordinates) {
    const geoResolved = resolveMarket({ coordinates }, catalog);
    if (geoResolved.source === "geo") {
      return {
        market: geoResolved.market,
        source: geoResolved.source,
        coordinates,
        searchLocation: coordinates,
      };
    }
  }

  if (envMarketId) {
    const resolved = resolveMarket({ marketId: envMarketId, coordinates }, catalog);
    return {
      market: resolved.market,
      source: resolved.source,
      coordinates,
      searchLocation: resolveSearchLocation(resolved.market, coordinates),
    };
  }

  const resolved = resolveMarket({ coordinates }, catalog);
  return {
    market: resolved.market,
    source: resolved.source,
    coordinates,
    searchLocation: resolveSearchLocation(resolved.market, coordinates),
  };
}

const MarketContext = createContext<ActiveMarketState | null>(null);

export function MarketProvider({ children }: { children: ReactNode }) {
  const [manualMarketId, setManualMarketId] = useState<string | null>(null);
  const [coordinates, setCoordinates] = useState<MarketLocation | null>(null);
  const [catalog, setCatalog] = useState<MarketConfig[]>(listMarkets());
  const [loading, setLoading] = useState(true);
  const [locationDenied, setLocationDenied] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function loadCatalog() {
      try {
        const client = createPublicApiClient();
        const markets = await client.getMarkets();
        if (!cancelled && markets.length > 0) {
          setCatalog(markets);
        }
      } catch {
        // Keep JSON fallback catalog.
      }
    }

    void loadCatalog();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;

    async function bootstrap() {
      const storedMarketId = await readStoredMarketId();

      if (cancelled) {
        return;
      }

      if (storedMarketId) {
        setManualMarketId(storedMarketId);
      }

      const permission = await Location.requestForegroundPermissionsAsync();

      if (cancelled) {
        return;
      }

      if (permission.status !== "granted") {
        setLocationDenied(true);
        setLoading(false);
        return;
      }

      try {
        const position = await Location.getCurrentPositionAsync({
          accuracy: Location.Accuracy.Balanced,
        });

        if (cancelled) {
          return;
        }

        setCoordinates({
          lat: position.coords.latitude,
          lng: position.coords.longitude,
        });
        setLocationDenied(false);
      } catch {
        if (!cancelled) {
          setLocationDenied(true);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void bootstrap();

    return () => {
      cancelled = true;
    };
  }, []);

  const setMarketId = useCallback(async (marketId: string) => {
    setManualMarketId(marketId);
    await writeStoredMarketId(marketId);
  }, []);

  const resolved = resolveFromInputs(coordinates, manualMarketId ?? envMarketId, catalog);

  const value: ActiveMarketState = {
    ...resolved,
    loading,
    locationDenied,
    setMarketId,
  };

  return <MarketContext.Provider value={value}>{children}</MarketContext.Provider>;
}

export function useMarket(): ActiveMarketState {
  const context = useContext(MarketContext);

  if (!context) {
    throw new Error("useMarket must be used within MarketProvider");
  }

  return context;
}
