import { Check, Minus } from "lucide-react";
import {
  PLAN_COMPARISON,
  PLAN_PRICING,
  type SubscriptionTier,
} from "@adeni/shared";
import { BusinessPortalCard } from "@/components/business-portal-card";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/cn";

type Props = {
  currentTier?: SubscriptionTier;
};

function FeatureCell({ value }: { value: string | boolean }) {
  if (value === true) {
    return <Check className="mx-auto h-4 w-4 text-accent" aria-label="Included" />;
  }
  if (value === false) {
    return <Minus className="mx-auto h-4 w-4 text-muted-foreground/50" aria-label="Not included" />;
  }
  return <span className="text-sm text-foreground">{value}</span>;
}

export function BusinessPlanComparison({ currentTier = "free" }: Props) {
  const tiers: SubscriptionTier[] = ["free", "pro", "business"];

  return (
    <div className="space-y-6">
      <div className="grid gap-4 lg:grid-cols-3">
        {tiers.map((tier) => {
          const plan = PLAN_PRICING[tier];
          const isCurrent = tier === currentTier;
          return (
            <BusinessPortalCard
              key={tier}
              className={cn(
                "relative flex flex-col",
                isCurrent && "ring-2 ring-accent/40",
              )}
            >
              {isCurrent ? (
                <span className="absolute -top-2.5 left-4 rounded-full bg-accent px-2.5 py-0.5 text-[10px] font-bold uppercase tracking-wider text-white">
                  Current plan
                </span>
              ) : null}
              <p className="text-xs font-bold uppercase tracking-widest text-accent">{plan.name}</p>
              <p className="mt-2 text-2xl font-bold text-foreground">{plan.priceLabel}</p>
              <p className="mt-2 text-sm leading-relaxed text-muted">{plan.description}</p>
              {tier === "free" ? (
                <p className="mt-auto pt-6 text-xs text-muted-foreground">Default for all new businesses</p>
              ) : (
                <Button
                  href="/business/plan"
                  variant={isCurrent ? "secondary" : "primary"}
                  className="mt-6 w-full"
                  disabled={isCurrent}
                >
                  {isCurrent ? "Current plan" : "Upgrade (coming soon)"}
                </Button>
              )}
            </BusinessPortalCard>
          );
        })}
      </div>

      <BusinessPortalCard padding="lg">
        <h3 className="text-sm font-bold uppercase tracking-widest text-accent">Feature comparison</h3>
        <div className="mt-6 overflow-x-auto">
          <table className="w-full min-w-[640px] text-left text-sm">
            <thead>
              <tr className="border-b border-border text-muted">
                <th className="pb-3 pr-4 font-semibold">Feature</th>
                {tiers.map((tier) => (
                  <th key={tier} className="pb-3 px-3 text-center font-semibold capitalize">
                    {tier}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {PLAN_COMPARISON.map((row) => (
                <tr key={row.label} className="border-b border-border/60 last:border-0">
                  <td className="py-3 pr-4 text-muted">{row.label}</td>
                  <td className="py-3 px-3 text-center">
                    <FeatureCell value={row.free} />
                  </td>
                  <td className="py-3 px-3 text-center">
                    <FeatureCell value={row.pro} />
                  </td>
                  <td className="py-3 px-3 text-center">
                    <FeatureCell value={row.business} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <p className="mt-4 text-xs text-muted">
          Paid checkout via Paystack is planned for a future sprint. Admins can override tiers for pilot businesses.
        </p>
      </BusinessPortalCard>
    </div>
  );
}
