"use client";

import { Suspense, useEffect, useRef, useState } from "react";
import { usePathname, useSearchParams } from "next/navigation";

function isSameOriginNavigation(href: string, pathname: string) {
  if (!href || href.startsWith("#") || href.startsWith("mailto:") || href.startsWith("tel:")) {
    return false;
  }

  try {
    const url = new URL(href, window.location.href);
    if (url.origin !== window.location.origin) {
      return false;
    }

    const nextPath = `${url.pathname}${url.search}`;
    const currentPath = `${pathname}${window.location.search}`;
    return nextPath !== currentPath;
  } catch {
    return false;
  }
}

function NavigationProgressInner() {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const [active, setActive] = useState(false);
  const [progress, setProgress] = useState(0);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    setActive(false);
    setProgress(100);

    const completeTimer = window.setTimeout(() => {
      setProgress(0);
    }, 280);

    return () => window.clearTimeout(completeTimer);
  }, [pathname, searchParams]);

  useEffect(() => {
    function handleClick(event: MouseEvent) {
      if (event.defaultPrevented || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
        return;
      }

      const anchor = (event.target as HTMLElement | null)?.closest("a");
      if (!anchor || anchor.target === "_blank" || anchor.hasAttribute("download")) {
        return;
      }

      const href = anchor.getAttribute("href");
      if (!href || !isSameOriginNavigation(href, pathname)) {
        return;
      }

      setActive(true);
      setProgress(18);
    }

    document.addEventListener("click", handleClick, true);
    return () => document.removeEventListener("click", handleClick, true);
  }, [pathname]);

  useEffect(() => {
    if (!active) {
      if (timerRef.current) {
        clearInterval(timerRef.current);
        timerRef.current = null;
      }
      return;
    }

    timerRef.current = setInterval(() => {
      setProgress((current) => {
        if (current >= 92) {
          return current;
        }

        const increment = 4 + Math.random() * 10;
        return Math.min(current + increment, 92);
      });
    }, 320);

    return () => {
      if (timerRef.current) {
        clearInterval(timerRef.current);
        timerRef.current = null;
      }
    };
  }, [active]);

  if (progress <= 0 && !active) {
    return null;
  }

  return (
    <div
      className="adeni-nav-progress pointer-events-none fixed inset-x-0 top-0 z-[300] h-[3px]"
      aria-hidden
    >
      <div
        className="adeni-nav-progress-bar h-full rounded-r-full bg-accent shadow-[0_0_12px_rgba(64,145,108,0.45)] transition-[width] duration-200 ease-out"
        style={{ width: `${progress}%` }}
      />
    </div>
  );
}

export function NavigationProgress() {
  return (
    <Suspense fallback={null}>
      <NavigationProgressInner />
    </Suspense>
  );
}
