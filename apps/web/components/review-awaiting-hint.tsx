"use client";

import { MessageCirclePlus } from "lucide-react";

type Props = {
  label: string;
};

export function ReviewAwaitingHint({ label }: Props) {
  return (
    <span
      className="group/review-hint relative inline-flex shrink-0"
      title={label}
      aria-label={label}
    >
      <span className="inline-flex h-6 w-6 items-center justify-center rounded-full text-muted transition-colors hover:bg-subtle hover:text-accent">
        <MessageCirclePlus className="h-4 w-4" aria-hidden />
      </span>
      <span
        role="tooltip"
        className="pointer-events-none absolute bottom-full left-1/2 z-20 mb-2 hidden w-max max-w-[13rem] -translate-x-1/2 rounded-lg border border-border bg-surface px-2.5 py-1.5 text-center text-xs font-medium leading-snug text-foreground opacity-0 shadow-md transition-opacity group-hover/review-hint:opacity-100 sm:block"
      >
        {label}
      </span>
    </span>
  );
}
