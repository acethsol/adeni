"use client";

import { Sparkles } from "lucide-react";

type Props = {
  badgeLabel: string;
  tooltipLabel: string;
};

export function NewReviewsBadge({ badgeLabel, tooltipLabel }: Props) {
  return (
    <span
      className="group/new-badge relative inline-flex"
      title={tooltipLabel}
      aria-label={tooltipLabel}
    >
      <span className="inline-flex items-center gap-1 rounded-full border border-primary/25 bg-surface/95 px-2.5 py-1 text-xs font-semibold text-primary shadow-sm backdrop-blur-sm">
        <Sparkles className="h-3.5 w-3.5" aria-hidden />
        {badgeLabel}
      </span>
      <span
        role="tooltip"
        className="pointer-events-none absolute right-0 top-full z-20 mt-2 hidden w-max max-w-[13rem] rounded-lg border border-border bg-surface px-2.5 py-1.5 text-xs font-medium leading-snug text-foreground opacity-0 shadow-md transition-opacity group-hover/new-badge:opacity-100 sm:block"
      >
        {tooltipLabel}
      </span>
    </span>
  );
}
