"use client";

import { useEffect, useRef } from "react";
import { useRouter } from "next/navigation";

const COORDS_COOKIE_NAME = "adeni_coords";

function readCoordsCookie(): boolean {
  return document.cookie.split(";").some((part) => part.trim().startsWith(`${COORDS_COOKIE_NAME}=`));
}

export function MarketGeoSync() {
  const router = useRouter();
  const attemptedRef = useRef(false);

  useEffect(() => {
    if (attemptedRef.current || readCoordsCookie()) {
      return;
    }

    attemptedRef.current = true;

    if (!navigator.geolocation) {
      return;
    }

    navigator.geolocation.getCurrentPosition(
      async (position) => {
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
            router.refresh();
          }
        } catch {
          // Ignore — fallback market still works.
        }
      },
      () => {
        // Permission denied or unavailable — keep market fallback.
      },
      {
        enableHighAccuracy: false,
        maximumAge: 60_000,
        timeout: 10_000,
      },
    );
  }, [router]);

  return null;
}
