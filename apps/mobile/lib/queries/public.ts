import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
import {
  DISCOVERY_PAGE_SIZE,
  queryKeys,
  staleTimes,
  type DiscoveryResponse,
} from "@adeni/shared";
import { createPublicApiClient } from "@/lib/api";

export function useCategories() {
  return useQuery({
    queryKey: queryKeys.categories,
    queryFn: async () => {
      const client = createPublicApiClient();
      return client.getCategories();
    },
    staleTime: staleTimes.categories,
  });
}

export function useInfiniteDiscovery(params: {
  lat: number;
  lng: number;
  market: string;
  category?: string | null;
  q?: string | null;
  pageSize?: number;
  sort?: "distance" | "featured";
  enabled?: boolean;
}) {
  const {
    enabled = true,
    pageSize = DISCOVERY_PAGE_SIZE,
    sort = "distance",
    ...discoveryParams
  } = params;

  return useInfiniteQuery({
    queryKey: queryKeys.discovery({
      ...discoveryParams,
      pageSize,
      sort,
    }),
    initialPageParam: 1,
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
