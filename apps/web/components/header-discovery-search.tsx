"use client";

import { Suspense } from "react";
import { DiscoverySearch, type DiscoverySearchVariant } from "@/components/discovery-search";

function DiscoverySearchFallback({ variant }: { variant: DiscoverySearchVariant }) {
  if (variant === "compact") {
    return (
      <div className="h-11 w-full animate-pulse rounded-full border border-border bg-surface shadow-sm" />
    );
  }

  if (variant === "hero") {
    return (
      <div className="h-14 w-full animate-pulse rounded-full border border-border bg-surface shadow-sm" />
    );
  }

  return (
    <div className="h-10 w-full animate-pulse rounded-full border border-border bg-surface" />
  );
}

type Props = {
  className?: string;
  marketName?: string;
  variant?: DiscoverySearchVariant;
};

export function HeaderDiscoverySearch({
  className,
  marketName,
  variant = "default",
}: Props) {
  return (
    <Suspense fallback={<DiscoverySearchFallback variant={variant} />}>
      <DiscoverySearch className={className} marketName={marketName} variant={variant} />
    </Suspense>
  );
}
