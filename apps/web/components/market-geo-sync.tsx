"use client";

import { useEffect, useRef } from "react";
import { useRouter } from "next/navigation";

export function MarketGeoSync() {
  const router = useRouter();
  const attemptedRef = useRef(false);

  useEffect(() => {
    if (attemptedRef.current) {
      return;
    }

    attemptedRef.current = true;

    const syncFromPosition = async (position: GeolocationPosition) => {
      try {
        const response = await fetch("/api/market/geo", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            lat: position.coords.latitude,
            lng: position.coords.longitude,
          }),
        });

        if (response.ok) {
          // Avoid router.refresh() on discover — it cancels in-flight infinite scroll.
          if (!window.location.pathname.startsWith("/discover")) {
            router.refresh();
          }
        }
      } catch {
        // Ignore — fallback market still works.
      }
    };

    if (!navigator.geolocation) {
      return;
    }

    // Always request fresh coordinates so geo can override dev env defaults.
    navigator.geolocation.getCurrentPosition(
      (position) => {
        void syncFromPosition(position);
      },
      () => {
        // Permission denied or unavailable — keep market fallback.
      },
      {
        enableHighAccuracy: false,
        maximumAge: 0,
        timeout: 10_000,
      },
    );
  }, [router]);

  return null;
}
