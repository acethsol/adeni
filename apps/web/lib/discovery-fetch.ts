import type { DiscoveryResponse } from "@adeni/shared";
import { DISCOVERY_PAGE_SIZE } from "@adeni/shared";

type FetchDiscoveryPageInput = {
  lat: number;
  lng: number;
  market: string;
  category?: string | null;
  q?: string | null;
  page: number;
  pageSize?: number;
  sort?: "distance" | "featured";
  minRating?: number | null;
};

/** Browser-safe discovery fetch via same-origin API proxy (no server actions). */
export async function fetchDiscoveryPage(
  input: FetchDiscoveryPageInput,
): Promise<DiscoveryResponse> {
  const query = new URLSearchParams({
    lat: String(input.lat),
    lng: String(input.lng),
    page: String(input.page),
    pageSize: String(input.pageSize ?? DISCOVERY_PAGE_SIZE),
    sort: input.sort ?? "distance",
  });

  if (input.market) {
    query.set("market", input.market);
  }

  if (input.category) {
    query.set("category", input.category);
  }

  if (input.q) {
    query.set("q", input.q);
  }

  if (input.minRating) {
    query.set("minRating", String(input.minRating));
  }

  const response = await fetch(`/api/v1/discovery?${query.toString()}`, {
    method: "GET",
    headers: { Accept: "application/json" },
    cache: "no-store",
  });

  if (!response.ok) {
    throw new Error(`Discovery request failed (${response.status})`);
  }

  return response.json() as Promise<DiscoveryResponse>;
}
