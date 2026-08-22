"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";

/**
 * Renders a floating label via a portal to `document.body` so it can escape
 * a scroll-clipped ancestor (e.g. the collapsed sidebar rail) without
 * expanding that ancestor's scrollable content box.
 */
export function useSidebarTooltip<T extends HTMLElement = HTMLElement>(label: string, enabled: boolean) {
  const ref = useRef<T>(null);
  const [coords, setCoords] = useState<{ top: number; left: number } | null>(null);
  const [mounted, setMounted] = useState(false);

  useEffect(() => setMounted(true), []);

  useEffect(() => {
    if (!enabled) setCoords(null);
  }, [enabled]);

  const show = useCallback(() => {
    if (!enabled) return;
    const rect = ref.current?.getBoundingClientRect();
    if (!rect) return;
    setCoords({ top: rect.top + rect.height / 2, left: rect.right + 10 });
  }, [enabled]);

  const hide = useCallback(() => setCoords(null), []);

  const tooltip =
    mounted && enabled && coords
      ? createPortal(
          <span
            role="tooltip"
            className="pointer-events-none fixed z-[100] -translate-y-1/2 whitespace-nowrap rounded-lg bg-foreground px-2.5 py-1.5 text-xs font-semibold text-white shadow-lg"
            style={{ top: coords.top, left: coords.left }}
          >
            {label}
          </span>,
          document.body,
        )
      : null;

  return { ref, onMouseEnter: show, onMouseLeave: hide, onFocus: show, onBlur: hide, tooltip };
}
