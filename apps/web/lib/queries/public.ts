"use client";

import { useInfiniteQuery } from "@tanstack/react-query";
import {
  DISCOVERY_PAGE_SIZE,
  queryKeys,
  staleTimes,
  type DiscoveryResponse,
} from "@adeni/shared";
import { createPublicApiClient } from "@/lib/public-api";

export function useInfiniteDiscovery(params: {
  lat: number;
  lng: number;
  market: string;
  category?: string | null;
  q?: string | null;
  pageSize?: number;
  sort?: "distance" | "featured";
  enabled?: boolean;
  initialPage?: DiscoveryResponse;
}) {
  const {
    enabled = true,
    pageSize = DISCOVERY_PAGE_SIZE,
    sort = "distance",
    initialPage,
    ...discoveryParams
  } = params;

  return useInfiniteQuery({
    queryKey: queryKeys.discovery({
      ...discoveryParams,
      pageSize,
      sort,
    }),
    initialPageParam: 1,
    initialData: initialPage
      ? {
          pages: [initialPage],
          pageParams: [1],
        }
      : undefined,
    initialDataUpdatedAt: initialPage ? Date.now() : undefined,
    refetchOnMount: initialPage ? false : undefined,
    retry: false,
    queryFn: async ({ pageParam }): Promise<DiscoveryResponse> => {
      const client = createPublicApiClient();
      return client.searchDiscovery({
        lat: discoveryParams.lat,
        lng: discoveryParams.lng,
        market: discoveryParams.market,
        category: discoveryParams.category ?? undefined,
        q: discoveryParams.q ?? undefined,
        page: pageParam,
        pageSize,
        sort,
      });
    },
    getNextPageParam: (lastPage) => {
      const loaded = lastPage.page * lastPage.pageSize;
      return loaded < lastPage.totalCount ? lastPage.page + 1 : undefined;
    },
    staleTime: staleTimes.discovery,
    enabled,
  });
}
