"use client";

import { useCallback, useEffect, useState } from "react";
import type { AdminBusinessSummary, SubscriptionTier } from "@adeni/shared";
import { EmptyState } from "@/components/ui/empty-state";
import { useToast } from "@/contexts/toast-context";

type Props = {
  initialItems: AdminBusinessSummary[];
  initialError: string | null;
};

const TIERS: SubscriptionTier[] = ["free", "pro", "business"];

export function AdminSubscriptionPanel({ initialItems, initialError }: Props) {
  const toast = useToast();
  const [items, setItems] = useState(initialItems);
  const [error, setError] = useState(initialError);
  const [savingId, setSavingId] = useState<string | null>(null);

  const reload = useCallback(async () => {
    setError(null);
    try {
      const response = await fetch("/api/admin/businesses");
      if (!response.ok) throw new Error("Failed to load");
      const payload = (await response.json()) as { items: AdminBusinessSummary[] };
      setItems(payload.items ?? []);
    } catch {
      setError("Could not load businesses.");
    }
  }, []);

  useEffect(() => {
    if (initialError) void reload();
  }, [initialError, reload]);

  async function handleTierChange(business: AdminBusinessSummary, tier: SubscriptionTier) {
    if (tier === business.subscriptionTier) return;
    setSavingId(business.id);
    try {
      const response = await fetch(
        `/api/admin/businesses/${encodeURIComponent(business.id)}/subscription-tier`,
        {
          method: "PATCH",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ tier }),
        },
      );
      if (!response.ok) throw new Error("Failed");
      setItems((prev) =>
        prev.map((item) => (item.id === business.id ? { ...item, subscriptionTier: tier } : item)),
      );
      toast.success(`${business.name} set to ${tier}`);
    } catch {
      toast.error("Could not update subscription tier");
    } finally {
      setSavingId(null);
    }
  }

  if (error) {
    return <p className="mt-4 text-sm text-destructive">{error}</p>;
  }

  if (items.length === 0) {
    return (
      <EmptyState
        className="mt-4"
        title="No businesses yet"
        description="Registered businesses will appear here for pilot tier overrides."
      />
    );
  }

  return (
    <div className="mt-4 overflow-x-auto rounded-xl border border-border">
      <table className="w-full min-w-[640px] text-left text-sm">
        <thead className="bg-subtle/60 text-muted">
          <tr>
            <th className="px-4 py-3 font-semibold">Business</th>
            <th className="px-4 py-3 font-semibold">Slug</th>
            <th className="px-4 py-3 font-semibold">Status</th>
            <th className="px-4 py-3 font-semibold">Plan</th>
          </tr>
        </thead>
        <tbody>
          {items.map((business) => (
            <tr key={business.id} className="border-t border-border/60">
              <td className="px-4 py-3 font-medium text-foreground">{business.name}</td>
              <td className="px-4 py-3 text-muted">{business.slug || "—"}</td>
              <td className="px-4 py-3 text-muted">{business.status}</td>
              <td className="px-4 py-3">
                <select
                  className="rounded-lg border border-border bg-surface px-2 py-1.5 text-sm"
                  value={business.subscriptionTier}
                  disabled={savingId === business.id}
                  onChange={(event) =>
                    void handleTierChange(business, event.target.value as SubscriptionTier)
                  }
                  aria-label={`Subscription tier for ${business.name}`}
                >
                  {TIERS.map((tier) => (
                    <option key={tier} value={tier}>
                      {tier}
                    </option>
                  ))}
                </select>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
