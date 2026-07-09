"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { AuthNav } from "@/components/auth-nav";
import { HeaderDiscoverySearch } from "@/components/header-discovery-search";
import { canAccessMyBookings } from "@/lib/customer-access";
import { cn } from "@/lib/cn";

const navLinkClass =
  "rounded-full px-3 py-1.5 text-sm font-medium text-foreground transition-colors hover:bg-background hover:text-accent";

const SCROLL_THRESHOLD = 64;

export type PublicHeaderSearchMode = "hero-handoff" | "compact" | "inline";

type Props = {
  searchMode?: PublicHeaderSearchMode;
};

export function PublicHeader({ searchMode = "inline" }: Props) {
  const showMyBookings = canAccessMyBookings();
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    function onScroll() {
      setScrolled(window.scrollY > SCROLL_THRESHOLD);
    }

    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  const showCompactSearch =
    searchMode === "compact" || (searchMode === "hero-handoff" && scrolled);
  const showInlineSearch = searchMode === "inline";
  const showSearchRow = showCompactSearch || showInlineSearch;

  return (
    <header
      className={cn(
        "sticky top-0 z-40 border-b border-border bg-surface/95 backdrop-blur transition-[box-shadow,padding]",
        scrolled && showCompactSearch && "shadow-sm",
      )}
    >
      <div
        className={cn(
          "mx-auto max-w-5xl px-6 transition-all duration-300 ease-out",
          showSearchRow && !showCompactSearch ? "space-y-3 py-4" : "py-3",
          showCompactSearch && "py-2.5",
        )}
      >
        <div
          className={cn(
            "flex items-center gap-3 transition-all duration-300 ease-out",
            showCompactSearch && "gap-4 lg:gap-6",
          )}
        >
          <Link
            href="/"
            className={cn(
              "shrink-0 font-bold tracking-tight text-foreground transition-all duration-300",
              showCompactSearch ? "text-lg" : "text-xl",
            )}
          >
            Adeni
          </Link>

          {showCompactSearch ? (
            <div className="min-w-0 flex-1 transition-all duration-300 ease-out">
              <HeaderDiscoverySearch variant="compact" />
            </div>
          ) : null}

          <nav className="flex shrink-0 items-center gap-1 sm:gap-2">
            <Link href="/discover" className={cn(navLinkClass, "hidden sm:inline-flex")}>
              Discover
            </Link>
            {showMyBookings ? (
              <Link href="/my-bookings" className={cn(navLinkClass, "hidden md:inline-flex")}>
                My bookings
              </Link>
            ) : null}
            <Link href="/business" className={cn(navLinkClass, "hidden lg:inline-flex")}>
              For business
            </Link>
            <AuthNav />
          </nav>
        </div>

        {showInlineSearch ? (
          <div className="transition-opacity duration-200">
            <HeaderDiscoverySearch variant="default" />
          </div>
        ) : null}
      </div>
    </header>
  );
}
