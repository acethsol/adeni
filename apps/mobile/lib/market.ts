import { useEffect, useState } from "react";
import * as Location from "expo-location";
import {
  resolveMarket,
  type MarketConfig,
  type MarketLocation,
  type ResolvedMarket,
} from "@adeni/shared";

type ActiveMarketState = {
  market: MarketConfig;
  source: ResolvedMarket["source"];
  coordinates: MarketLocation | null;
  loading: boolean;
};

const envMarketId = process.env.EXPO_PUBLIC_ADENI_MARKET;

export function useActiveMarket(): ActiveMarketState {
  const [state, setState] = useState<ActiveMarketState>(() => {
    const resolved = resolveMarket({ marketId: envMarketId });
    return {
      market: resolved.market,
      source: resolved.source,
      coordinates: null,
      loading: !envMarketId,
    };
  });

  useEffect(() => {
    if (envMarketId) {
      return;
    }

    let cancelled = false;

    async function syncLocation() {
      const permission = await Location.requestForegroundPermissionsAsync();
      if (cancelled || permission.status !== "granted") {
        setState((current) => ({ ...current, loading: false }));
        return;
      }

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
      const resolved = resolveMarket({ coordinates });

      setState({
        market: resolved.market,
        source: resolved.source,
        coordinates,
        loading: false,
      });
    }

    void syncLocation().catch(() => {
      if (!cancelled) {
        setState((current) => ({ ...current, loading: false }));
      }
    });

    return () => {
      cancelled = true;
    };
  }, []);

  return state;
}
