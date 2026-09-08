"use client";

import Link from "next/link";
import { Bell } from "lucide-react";
import { cn } from "@/lib/cn";
import { usePendingBookingsCount } from "@/lib/queries/portal-badges";

export function BusinessPortalBell({ className }: { className?: string }) {
  const { data: count = null } = usePendingBookingsCount();

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
