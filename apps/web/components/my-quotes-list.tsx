"use client";

import { useCallback, useEffect, useState } from "react";
import { FileText } from "lucide-react";
import type { QuoteRequestResponse } from "@adeni/shared";
import { QUOTE_STATUS_LABELS } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { EmptyState } from "@/components/ui/empty-state";
import { SkeletonList } from "@/components/ui/skeleton";
import { useActionLoading } from "@/contexts/action-loading-context";
import { useConfirm } from "@/contexts/confirm-context";
import { useToast } from "@/contexts/toast-context";
import { useApiErrorMessage } from "@/lib/api-error";

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(value));
}

function formatMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`;
  }
}

export function MyQuotesList() {
  const { run } = useActionLoading();
  const confirm = useConfirm();
  const toast = useToast();
  const { formatApiError } = useApiErrorMessage();

  const [quotes, setQuotes] = useState<QuoteRequestResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionId, setActionId] = useState<string | null>(null);

  const loadQuotes = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await fetch("/api/quotes");
      if (!response.ok) {
        throw new Error("Could not load your quotes.");
      }
      const payload = (await response.json()) as { items: QuoteRequestResponse[] };
      setQuotes(payload.items ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load your quotes.");
      setQuotes([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadQuotes();
  }, [loadQuotes]);

  async function handleAccept(quote: QuoteRequestResponse) {
    const confirmed = await confirm({
      title: "Accept this quote?",
      description: "We'll create a booking with the proposed schedule.",
      confirmLabel: "Accept quote",
    });
    if (!confirmed) {
      return;
    }

    setActionId(quote.id);

    try {
      await run("Accepting quote…", async () => {
        const response = await fetch(`/api/quotes/${encodeURIComponent(quote.id)}/accept`, {
          method: "POST",
        });
        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(formatApiError(payload, "Could not accept quote."));
        }

        setQuotes((current) =>
          current.map((item) => (item.id === quote.id ? (payload as QuoteRequestResponse) : item)),
        );
        toast.success("Quote accepted — booking created");
      });
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not accept quote.");
    } finally {
      setActionId(null);
    }
  }

  async function handleDecline(quote: QuoteRequestResponse) {
    const confirmed = await confirm({
      title: "Decline this quote?",
      description: "The business will be notified that you passed on this offer.",
      confirmLabel: "Decline quote",
      tone: "destructive",
    });
    if (!confirmed) {
      return;
    }

    setActionId(quote.id);

    try {
      await run("Declining quote…", async () => {
        const response = await fetch(`/api/quotes/${encodeURIComponent(quote.id)}/decline`, {
          method: "POST",
        });
        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(formatApiError(payload, "Could not decline quote."));
        }

        setQuotes((current) =>
          current.map((item) => (item.id === quote.id ? (payload as QuoteRequestResponse) : item)),
        );
        toast.success("Quote declined");
      });
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not decline quote.");
    } finally {
      setActionId(null);
    }
  }

  if (loading) {
    return <SkeletonList count={3} />;
  }

  if (error) {
    return <Callout tone="error">{error}</Callout>;
  }

  if (quotes.length === 0) {
    return (
      <EmptyState
        icon={<FileText className="h-6 w-6" aria-hidden />}
        title="No quote requests yet"
        description="When you request quotes from businesses, they'll show up here."
        actionLabel="Discover businesses"
        actionHref="/discover"
      />
    );
  }

  return (
    <ul className="space-y-3">
      {quotes.map((quote) => (
        <li key={quote.id} className="rounded-xl border border-border bg-surface p-5 shadow-sm">
          <p className="text-xs font-semibold uppercase tracking-wide text-muted">
            {QUOTE_STATUS_LABELS[quote.status] ?? quote.status}
          </p>
          <p className="mt-2 text-sm leading-relaxed text-foreground">{quote.description}</p>
          {quote.serviceAddress ? (
            <p className="mt-2 text-sm text-muted">Service at: {quote.serviceAddress}</p>
          ) : null}
          {quote.photoUrls && quote.photoUrls.length > 0 ? (
            <div className="mt-3 flex flex-wrap gap-2">
              {quote.photoUrls.map((url) => (
                <img key={url} src={url} alt="" className="h-16 w-16 rounded-lg object-cover" />
              ))}
            </div>
          ) : null}
          <time className="mt-2 block text-xs text-muted-foreground">
            Requested {formatDateTime(quote.createdAt)}
          </time>

          {quote.status === "quoted" && quote.quotedAmount != null && quote.quotedCurrency ? (
            <div className="mt-4 rounded-lg border border-accent/20 bg-accent/5 p-4">
              <p className="text-lg font-bold text-foreground">
                {formatMoney(quote.quotedAmount, quote.quotedCurrency)}
              </p>
              {quote.proposedStartAt && quote.proposedEndAt ? (
                <p className="mt-1 text-sm text-muted">
                  Proposed: {formatDateTime(quote.proposedStartAt)} – {formatDateTime(quote.proposedEndAt)}
                </p>
              ) : null}
              {quote.quoteNotes ? (
                <p className="mt-2 text-sm text-muted-foreground">{quote.quoteNotes}</p>
              ) : null}
              <div className="mt-4 flex flex-wrap gap-2">
                <Button
                  size="sm"
                  onClick={() => void handleAccept(quote)}
                  loading={actionId === quote.id}
                  loadingLabel="Accepting…"
                >
                  Accept quote
                </Button>
                <Button
                  size="sm"
                  variant="secondary"
                  onClick={() => void handleDecline(quote)}
                  disabled={actionId === quote.id}
                >
                  Decline
                </Button>
              </div>
            </div>
          ) : null}

          {quote.status === "accepted" && quote.bookingId ? (
            <p className="mt-3 text-sm text-accent">
              Booking created.{" "}
              <a href="/my-bookings" className="font-semibold underline">
                View my bookings
              </a>
            </p>
          ) : null}
        </li>
      ))}
    </ul>
  );
}
