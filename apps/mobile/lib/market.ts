import { useEffect, useState } from "react";
import * as Location from "expo-location";
import {
  resolveMarket,
  resolveSearchLocation,
  type MarketConfig,
  type MarketLocation,
  type ResolvedMarket,
} from "@adeni/shared";

export type ActiveMarketState = {
  market: MarketConfig;
  source: ResolvedMarket["source"];
  coordinates: MarketLocation | null;
  searchLocation: MarketLocation;
  loading: boolean;
  locationDenied: boolean;
};

const envMarketId = process.env.EXPO_PUBLIC_ADENI_MARKET?.trim() || undefined;

function resolveFromInputs(
  coordinates: MarketLocation | null,
  explicitMarketId?: string,
): Pick<ActiveMarketState, "market" | "source" | "coordinates" | "searchLocation"> {
  if (explicitMarketId) {
    const resolved = resolveMarket({ marketId: explicitMarketId, coordinates });
    return {
      market: resolved.market,
      source: resolved.source,
      coordinates,
      searchLocation: resolveSearchLocation(resolved.market, coordinates),
    };
  }

  if (coordinates) {
    const geoResolved = resolveMarket({ coordinates });
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
    const resolved = resolveMarket({ marketId: envMarketId, coordinates });
    return {
      market: resolved.market,
      source: resolved.source,
      coordinates,
      searchLocation: resolveSearchLocation(resolved.market, coordinates),
    };
  }

  const resolved = resolveMarket({ coordinates });
  return {
    market: resolved.market,
    source: resolved.source,
    coordinates,
    searchLocation: resolveSearchLocation(resolved.market, coordinates),
  };
}

export function useActiveMarket(): ActiveMarketState {
  const [state, setState] = useState<ActiveMarketState>(() => {
    const initial = resolveFromInputs(null);
    return {
      ...initial,
      loading: true,
      locationDenied: false,
    };
  });

  useEffect(() => {
    let cancelled = false;

    async function syncLocation() {
      const permission = await Location.requestForegroundPermissionsAsync();
      if (cancelled) {
        return;
      }

      if (permission.status !== "granted") {
        setState({
          ...resolveFromInputs(null),
          loading: false,
          locationDenied: true,
        });
        return;
      }

      try {
        const position = await Location.getCurrentPositionAsync({
          accuracy: Location.Accuracy.Balanced,
        });

        if (cancelled) {
          return;
        }

        const coordinates = {
          lat: position.coords.latitude,
          lng: position.coords.longitude,
        };

        setState({
          ...resolveFromInputs(coordinates),
          loading: false,
          locationDenied: false,
        });
      } catch {
        if (!cancelled) {
          setState({
            ...resolveFromInputs(null),
            loading: false,
            locationDenied: true,
          });
        }
      }
    }

    void syncLocation();

    return () => {
      cancelled = true;
    };
  }, []);

  return state;
}
