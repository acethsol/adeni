"use client";

import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { HERO_SEARCH_ANCHOR_ID } from "@/lib/hero-search";

const HEADER_HEIGHT = 72;

export function useHeroSearchPinned(enabled: boolean): boolean {
  const pathname = usePathname();
  const [pinned, setPinned] = useState(false);

  useEffect(() => {
    if (!enabled) {
      setPinned(false);
      return;
    }

    let rafId = 0;

    function measure() {
      const target = document.getElementById(HERO_SEARCH_ANCHOR_ID);
      if (!target) {
        return false;
      }

      const rect = target.getBoundingClientRect();
      setPinned(rect.bottom <= HEADER_HEIGHT);
      return true;
    }

    function scheduleMeasure() {
      cancelAnimationFrame(rafId);
      rafId = requestAnimationFrame(measure);
    }

    scheduleMeasure();

    const retryId = window.setInterval(() => {
      if (measure()) {
        window.clearInterval(retryId);
      }
    }, 100);

    window.addEventListener("scroll", scheduleMeasure, { passive: true });
    window.addEventListener("resize", scheduleMeasure, { passive: true });

    return () => {
      cancelAnimationFrame(rafId);
      window.clearInterval(retryId);
      window.removeEventListener("scroll", scheduleMeasure);
      window.removeEventListener("resize", scheduleMeasure);
    };
  }, [enabled, pathname]);

  return pinned;
}
