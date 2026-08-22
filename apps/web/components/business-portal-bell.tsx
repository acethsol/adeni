"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { Bell } from "lucide-react";
import type { BookingResponse } from "@adeni/shared";
import { cn } from "@/lib/cn";

export function BusinessPortalBell({ className }: { className?: string }) {
  const pathname = usePathname();
  const [count, setCount] = useState<number | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const response = await fetch("/api/business/bookings", { cache: "no-store" });
        if (!response.ok) return;
        const payload = (await response.json()) as { items: BookingResponse[] };
        const pending = (payload.items ?? []).filter((item) => item.status === 0).length;
        if (!cancelled) setCount(pending);
      } catch {
        if (!cancelled) setCount(null);
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [pathname]);

  return (
    <Link
      href="/business/bookings"
      aria-label={count ? `${count} pending bookings` : "Bookings"}
      className={cn(
        "relative inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-muted transition-colors hover:bg-subtle hover:text-foreground",
        className,
      )}
    >
      <Bell className="h-4.5 w-4.5" aria-hidden />
      {count ? (
        <span className="absolute right-0.5 top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-destructive px-1 text-[10px] font-bold leading-none text-white">
          {count > 9 ? "9+" : count}
        </span>
      ) : null}
    </Link>
  );
}
