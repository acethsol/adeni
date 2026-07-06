import { useQuery } from "@tanstack/react-query";
import { queryKeys, staleTimes } from "@adeni/shared";
import { useAuth } from "@/contexts/auth-context";

export function useMyBookings(enabled = true) {
  const { createApiClient } = useAuth();

  return useQuery({
    queryKey: queryKeys.myBookings,
    queryFn: async () => {
      const client = createApiClient("customer");
      return client.getMyBookings();
    },
    staleTime: staleTimes.bookings,
    enabled,
  });
}
