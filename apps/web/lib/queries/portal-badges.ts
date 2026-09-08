"use client";

import { useQuery } from "@tanstack/react-query";
import { queryKeys, staleTimes } from "@adeni/shared";
import type { BookingResponse } from "@adeni/shared";

export function usePendingBookingsCount() {
  return useQuery({
    queryKey: queryKeys.businessPendingBookingsCount(),
    queryFn: async (): Promise<number> => {
      const response = await fetch("/api/business/bookings", { cache: "no-store" });
      if (!response.ok) {
        throw new Error("Could not load bookings.");
      }

      const payload = (await response.json()) as { items: BookingResponse[] };
      return (payload.items ?? []).filter((item) => item.status === 0).length;
    },
    staleTime: staleTimes.bookings,
    refetchOnWindowFocus: false,
  });
}

export function useUnreadMessagesCount() {
  return useQuery({
    queryKey: queryKeys.businessUnreadMessagesCount(),
    queryFn: async (): Promise<number> => {
      const response = await fetch("/api/business/messages/unread-count", { cache: "no-store" });
      if (!response.ok) {
        throw new Error("Could not load unread count.");
      }

      const payload = (await response.json()) as { count: number };
      return payload.count ?? 0;
    },
    staleTime: staleTimes.messages,
    refetchOnWindowFocus: false,
  });
}
