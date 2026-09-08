"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { MessageCircle } from "lucide-react";
import { cn } from "@/lib/cn";

export function BusinessPortalMessagesBell({ className }: { className?: string }) {
  const pathname = usePathname();
  const [count, setCount] = useState<number | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const response = await fetch("/api/business/messages/unread-count", { cache: "no-store" });
        if (!response.ok) {
          if (!cancelled) setCount(null);
          return;
        }

        const payload = (await response.json()) as { count: number };
        if (!cancelled) setCount(payload.count ?? 0);
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
      href="/business/messages"
      aria-label={count ? `${count} unread messages` : "Messages"}
      className={cn(
        "relative inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-muted transition-colors hover:bg-subtle hover:text-foreground",
        className,
      )}
    >
      <MessageCircle className="h-4.5 w-4.5" aria-hidden />
      {count ? (
        <span className="absolute right-0.5 top-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-destructive px-1 text-[10px] font-bold leading-none text-white">
          {count > 9 ? "9+" : count}
        </span>
      ) : null}
    </Link>
  );
}
