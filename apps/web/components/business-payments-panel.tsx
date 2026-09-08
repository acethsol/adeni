"use client";

import { useCallback, useEffect, useState } from "react";
import type { BusinessProfile, PaymentLedgerEntry } from "@adeni/shared";
import { hasCapability, formatPrice } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { LoadingPanel } from "@/components/loading-panel";
import { useToast } from "@/contexts/toast-context";
import { useApiErrorMessage } from "@/lib/api-error";

type Props = {
  profile: BusinessProfile;
};

function whatsAppShareUrl(description: string, checkoutUrl: string) {
  const text = encodeURIComponent(`Pay ${description} via Adeni: ${checkoutUrl}`);
  return `https://wa.me/?text=${text}`;
}

export function BusinessPaymentsPanel({ profile }: Props) {
  const toast = useToast();
  const { formatApiError } = useApiErrorMessage();
  const [ledger, setLedger] = useState<PaymentLedgerEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");
  const [lastLink, setLastLink] = useState<{ checkoutUrl: string; description: string } | null>(null);

  const supportsDeposits = hasCapability(profile.capabilities, "deposits");

  const loadLedger = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const query = new URLSearchParams({ tenantId: profile.tenantId });
      const response = await fetch(`/api/business/payments?${query.toString()}`);
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(formatApiError(payload, "Could not load payments."));
      }
      setLedger((payload.items ?? []) as PaymentLedgerEntry[]);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load payments.");
    } finally {
      setLoading(false);
    }
  }, [formatApiError, profile.tenantId]);

  useEffect(() => {
    void loadLedger();
  }, [loadLedger]);

  async function handleCreateLink(event: React.FormEvent) {
    event.preventDefault();
    const parsedAmount = Number(amount);
    if (!Number.isFinite(parsedAmount) || parsedAmount <= 0 || !description.trim()) {
      setError("Enter a valid amount and description.");
      return;
    }

    setCreating(true);
    setError(null);
    try {
      const response = await fetch("/api/business/payments", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          tenantId: profile.tenantId,
          amount: parsedAmount,
          currency: profile.locations[0]?.marketId === "lagos" ? "NGN" : "NGN",
          description: description.trim(),
        }),
      });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(formatApiError(payload, "Could not create payment link."));
      }

      const checkoutUrl = typeof window !== "undefined"
        ? new URL(payload.checkoutUrl, window.location.origin).toString()
        : payload.checkoutUrl;

      setLastLink({ checkoutUrl, description: description.trim() });
      setAmount("");
      setDescription("");
      toast.success("Payment link created.");
      await loadLedger();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create payment link.");
    } finally {
      setCreating(false);
    }
  }

  async function handleRefund(entry: PaymentLedgerEntry) {
    if (entry.status !== "completed") {
      return;
    }

    if (!window.confirm(`Refund ${formatPrice(entry.amount, entry.currency)}?`)) {
      return;
    }

    try {
      const response = await fetch(`/api/business/payments/${entry.id}/refund`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ tenantId: profile.tenantId }),
      });
      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(formatApiError(payload, "Refund failed."));
      }
      toast.success("Refund initiated.");
      await loadLedger();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Refund failed.");
    }
  }

  if (!supportsDeposits) {
    return (
      <Callout tone="info">
        Payments are not enabled for your business category yet.
      </Callout>
    );
  }

  return (
    <div className="space-y-8">
      <section className="rounded-2xl border border-border bg-surface p-6 shadow-sm">
        <h2 className="text-lg font-semibold">Create payment link</h2>
        <p className="mt-1 text-sm text-muted">
          Share a Pay ₦X link with customers on WhatsApp. They pay via secure checkout and you get a receipt.
        </p>
        <form className="mt-4 grid gap-4 sm:grid-cols-2" onSubmit={(event) => void handleCreateLink(event)}>
          <label className="block text-sm">
            <span className="font-medium">Amount (NGN)</span>
            <input
              type="number"
              min="1"
              step="0.01"
              value={amount}
              onChange={(event) => setAmount(event.target.value)}
              className="mt-1 w-full rounded-lg border border-border px-3 py-2"
              required
            />
          </label>
          <label className="block text-sm sm:col-span-2">
            <span className="font-medium">Description</span>
            <input
              type="text"
              maxLength={500}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              placeholder="e.g. Fade + beard trim deposit"
              className="mt-1 w-full rounded-lg border border-border px-3 py-2"
              required
            />
          </label>
          <div className="sm:col-span-2">
            <Button type="submit" disabled={creating}>
              {creating ? "Creating…" : "Create link"}
            </Button>
          </div>
        </form>

        {lastLink ? (
          <div className="mt-4 rounded-xl border border-dashed border-accent/40 bg-subtle/40 p-4 text-sm">
            <p className="font-medium">Latest link</p>
            <p className="mt-1 break-all text-muted">{lastLink.checkoutUrl}</p>
            <div className="mt-3 flex flex-wrap gap-2">
              <Button
                type="button"
                variant="secondary"
                onClick={() => {
                  void navigator.clipboard.writeText(lastLink.checkoutUrl);
                  toast.success("Link copied.");
                }}
              >
                Copy link
              </Button>
              <a
                href={whatsAppShareUrl(lastLink.description, lastLink.checkoutUrl)}
                target="_blank"
                rel="noreferrer"
                className="inline-flex items-center rounded-full bg-[#25D366] px-4 py-2 text-sm font-semibold text-white"
              >
                Share on WhatsApp
              </a>
            </div>
          </div>
        ) : null}
      </section>

      <section className="rounded-2xl border border-border bg-surface p-6 shadow-sm">
        <h2 className="text-lg font-semibold">Payment history</h2>
        {error ? <Callout tone="error" className="mt-4">{error}</Callout> : null}
        {loading ? (
          <LoadingPanel message="Loading payments…" variant="card" className="mt-4" />
        ) : ledger.length === 0 ? (
          <p className="mt-4 text-sm text-muted">No payments yet.</p>
        ) : (
          <ul className="mt-4 divide-y divide-border">
            {ledger.map((entry) => (
              <li key={entry.id} className="flex flex-wrap items-start justify-between gap-3 py-4">
                <div>
                  <p className="font-medium">{formatPrice(entry.amount, entry.currency)}</p>
                  <p className="text-sm text-muted capitalize">
                    {entry.type} · {entry.status}
                  </p>
                  {entry.description ? (
                    <p className="mt-1 text-sm text-muted">{entry.description}</p>
                  ) : null}
                  <p className="mt-1 font-mono text-xs text-muted">{entry.providerReference}</p>
                </div>
                {entry.status === "completed" ? (
                  <Button type="button" variant="secondary" onClick={() => void handleRefund(entry)}>
                    Refund
                  </Button>
                ) : null}
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
