"use client";

import { useQuery } from "@tanstack/react-query";
import { queryKeys, staleTimes } from "@adeni/shared";
import { createApiClient } from "@/lib/adeni";

export function useCategories() {
  return useQuery({
    queryKey: queryKeys.categories,
    queryFn: async () => {
      const client = createApiClient();
      return client.getCategories();
    },
    staleTime: staleTimes.categories,
  });
}

export function useDiscovery(params: {
  lat: number;
  lng: number;
  market: string;
  category?: string | null;
  q?: string | null;
  enabled?: boolean;
}) {
  const { enabled = true, ...discoveryParams } = params;

  return useQuery({
    queryKey: queryKeys.discovery(discoveryParams),
    queryFn: async () => {
      const client = createApiClient();
      const result = await client.searchDiscovery({
        lat: discoveryParams.lat,
        lng: discoveryParams.lng,
        market: discoveryParams.market,
        category: discoveryParams.category ?? undefined,
        q: discoveryParams.q ?? undefined,
        pageSize: 50,
      });
      return result.items;
    },
    staleTime: staleTimes.discovery,
    enabled,
  });
}
