"use client";

import { Suspense } from "react";
import { DiscoverySearch } from "@/components/discovery-search";

function HeroDiscoverySearchFallback() {
  return (
    <div className="mx-auto h-14 w-full max-w-2xl animate-pulse rounded-full border border-border bg-surface shadow-sm" />
  );
}

type Props = {
  className?: string;
};

export function HeroDiscoverySearch({ className }: Props) {
  return (
    <Suspense fallback={<HeroDiscoverySearchFallback />}>
      <DiscoverySearch className={className} variant="hero" />
    </Suspense>
  );
}
