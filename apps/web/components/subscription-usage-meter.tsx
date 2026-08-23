"use client";

import Link from "next/link";
import { AlertTriangle } from "lucide-react";
import type { SubscriptionUsage } from "@adeni/shared";
import { formatBookingUsage, isNearBookingLimit } from "@adeni/shared";
import { BusinessPortalCard } from "@/components/business-portal-card";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/cn";

type Props = {
  usage: SubscriptionUsage;
  compact?: boolean;
};

export function SubscriptionUsageMeter({ usage, compact = false }: Props) {
  const limit = usage.bookingsLimitThisMonth;
  const used = usage.bookingsUsedThisMonth;
  const percent = limit ? Math.min(100, Math.round((used / limit) * 100)) : 0;
  const nearLimit = isNearBookingLimit(usage);
  const atLimit = limit != null && used >= limit;

  return (
    <BusinessPortalCard className={cn(compact && "p-4")}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-xs font-bold uppercase tracking-wider text-accent">Plan usage</p>
          <p className="mt-1 text-sm text-muted">{formatBookingUsage(usage)}</p>
        </div>
        <span className="rounded-full bg-subtle px-2.5 py-1 text-xs font-semibold uppercase tracking-wide text-foreground">
          {usage.tier}
        </span>
      </div>

      {limit != null ? (
        <div className="mt-4">
          <div className="h-2 overflow-hidden rounded-full bg-subtle">
            <div
              className={cn(
                "h-full rounded-full transition-all",
                atLimit ? "bg-destructive" : nearLimit ? "bg-amber-400" : "bg-accent",
              )}
              style={{ width: `${percent}%` }}
            />
          </div>
          <p className="mt-2 text-xs text-muted-foreground">{percent}% of monthly limit</p>
        </div>
      ) : (
        <p className="mt-3 text-xs text-muted-foreground">Unlimited bookings on your plan.</p>
      )}

      {(nearLimit || atLimit) && usage.tier === "free" ? (
        <div className="mt-4 flex items-start gap-2 rounded-xl border border-amber-200/80 bg-amber-50 px-3 py-2.5 text-sm text-amber-950 dark:border-amber-900/40 dark:bg-amber-950/30 dark:text-amber-100">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
          <div>
            <p className="font-semibold">{atLimit ? "Monthly limit reached" : "Almost at your limit"}</p>
            <p className="mt-0.5 text-xs opacity-90">
              Upgrade to Pro for unlimited bookings, messaging, and analytics.
            </p>
            <Button href="/business/plan" size="sm" className="mt-2">
              Compare plans
            </Button>
          </div>
        </div>
      ) : (
        <p className="mt-4 text-xs">
          <Link href="/business/plan" className="font-semibold text-accent hover:underline">
            View plans & upgrade
          </Link>
        </p>
      )}
    </BusinessPortalCard>
  );
}
