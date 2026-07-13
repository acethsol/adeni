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

    let observer: IntersectionObserver | null = null;
    let retryId = 0;

    function attach(target: HTMLElement) {
      observer = new IntersectionObserver(
        ([entry]) => {
          setPinned(!entry.isIntersecting);
        },
        {
          root: null,
          rootMargin: `-${HEADER_HEIGHT}px 0px 0px 0px`,
          threshold: 0,
        },
      );

      observer.observe(target);
    }

    function tryAttach() {
      const target = document.getElementById(HERO_SEARCH_ANCHOR_ID);
      if (!target) {
        return false;
      }

      attach(target);
      return true;
    }

    if (!tryAttach()) {
      retryId = window.setInterval(() => {
        if (tryAttach()) {
          window.clearInterval(retryId);
        }
      }, 100);
    }

    return () => {
      window.clearInterval(retryId);
      observer?.disconnect();
    };
  }, [enabled, pathname]);

  return pinned;
}
