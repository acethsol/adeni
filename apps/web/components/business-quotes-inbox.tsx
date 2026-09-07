"use client";

import { useCallback, useEffect, useState } from "react";
import { FileText } from "lucide-react";
import type { QuoteRequestResponse, ServiceOffering } from "@adeni/shared";
import { QUOTE_STATUS_LABELS } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { EmptyState } from "@/components/ui/empty-state";
import { Input, Textarea } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";
import { SkeletonList } from "@/components/ui/skeleton";
import { useActionLoading } from "@/contexts/action-loading-context";
import { useToast } from "@/contexts/toast-context";
import { useApiErrorMessage } from "@/lib/api-error";

type Props = {
  services: ServiceOffering[];
  defaultCurrency?: string;
};

function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  }).format(new Date(value));
}

function defaultProposedStart(): string {
  const date = new Date();
  date.setDate(date.getDate() + 2);
  date.setHours(10, 0, 0, 0);
  return date.toISOString();
}

function defaultProposedEnd(startIso: string, durationMinutes: number): string {
  const start = new Date(startIso);
  return new Date(start.getTime() + durationMinutes * 60_000).toISOString();
}

export function BusinessQuotesInbox({ services, defaultCurrency = "NGN" }: Props) {
  const { run } = useActionLoading();
  const toast = useToast();
  const { formatApiError } = useApiErrorMessage();

  const [quotes, setQuotes] = useState<QuoteRequestResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedQuote, setSelectedQuote] = useState<QuoteRequestResponse | null>(null);
  const [amount, setAmount] = useState("");
  const [currency, setCurrency] = useState(defaultCurrency);
  const [notes, setNotes] = useState("");
  const [serviceOfferingId, setServiceOfferingId] = useState("");
  const [proposedStartAt, setProposedStartAt] = useState(defaultProposedStart());
  const [proposedEndAt, setProposedEndAt] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const activeServices = services.filter((service) => service.isActive);

  const loadQuotes = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const response = await fetch("/api/business/quotes");
      if (!response.ok) {
        throw new Error("Could not load quote requests.");
      }
      const payload = (await response.json()) as { items: QuoteRequestResponse[] };
      setQuotes(payload.items ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load quote requests.");
      setQuotes([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadQuotes();
  }, [loadQuotes]);

  function openOfferModal(quote: QuoteRequestResponse) {
    const defaultService = activeServices[0];
    const start = defaultProposedStart();
    setSelectedQuote(quote);
    setAmount("");
    setCurrency(defaultCurrency);
    setNotes("");
    setServiceOfferingId(defaultService?.id ?? "");
    setProposedStartAt(start);
    setProposedEndAt(defaultService ? defaultProposedEnd(start, defaultService.durationMinutes) : "");
  }

  function closeOfferModal() {
    setSelectedQuote(null);
  }

  function handleServiceChange(nextServiceId: string) {
    setServiceOfferingId(nextServiceId);
    const service = activeServices.find((item) => item.id === nextServiceId);
    if (service) {
      setProposedEndAt(defaultProposedEnd(proposedStartAt, service.durationMinutes));
    }
  }

  async function handleSubmitOffer(event: React.FormEvent) {
    event.preventDefault();
    if (!selectedQuote) {
      return;
    }

    const parsedAmount = Number(amount);
    if (!amount.trim() || Number.isNaN(parsedAmount) || parsedAmount <= 0) {
      toast.error("Enter a valid quote amount.");
      return;
    }

    if (!serviceOfferingId) {
      toast.error("Select a service for this quote.");
      return;
    }

    if (!proposedStartAt || !proposedEndAt) {
      toast.error("Set proposed start and end times.");
      return;
    }

    setSubmitting(true);

    try {
      await run("Sending quote…", async () => {
        const response = await fetch(
          `/api/business/quotes/${encodeURIComponent(selectedQuote.id)}/offer`,
          {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
              amount: parsedAmount,
              currency: currency.trim().toUpperCase(),
              notes: notes.trim() || undefined,
              serviceOfferingId,
              proposedStartAt,
              proposedEndAt,
            }),
          },
        );

        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(formatApiError(payload, "Could not send quote."));
        }

        setQuotes((current) =>
          current.map((item) => (item.id === selectedQuote.id ? (payload as QuoteRequestResponse) : item)),
        );
        toast.success("Quote sent to customer");
        closeOfferModal();
      });
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not send quote.");
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    return <SkeletonList count={3} />;
  }

  if (error) {
    return <Callout tone="error">{error}</Callout>;
  }

  const pendingQuotes = quotes.filter((quote) => quote.status === "submitted");

  return (
    <div className="space-y-4">
      {pendingQuotes.length === 0 && quotes.length === 0 ? (
        <EmptyState
          icon={<FileText className="h-6 w-6" aria-hidden />}
          title="No quote requests yet"
          description="Customer job requests will appear here when they ask for a quote on your profile."
        />
      ) : (
        <ul className="space-y-3">
          {quotes.map((quote) => (
            <li key={quote.id} className="rounded-xl border border-border bg-surface p-4 shadow-sm">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0 flex-1">
                  <p className="text-xs font-semibold uppercase tracking-wide text-muted">
                    {QUOTE_STATUS_LABELS[quote.status] ?? quote.status}
                  </p>
                  <p className="mt-1 text-sm leading-relaxed text-foreground">{quote.description}</p>
                  {quote.serviceAddress ? (
                    <p className="mt-2 text-sm text-muted">Service at: {quote.serviceAddress}</p>
                  ) : null}
                  <time className="mt-2 block text-xs text-muted-foreground">
                    Requested {formatDateTime(quote.createdAt)}
                  </time>
                  {quote.status === "quoted" && quote.quotedAmount != null ? (
                    <p className="mt-2 text-sm font-semibold text-accent">
                      Your quote: {quote.quotedCurrency} {quote.quotedAmount.toLocaleString()}
                      {quote.proposedStartAt ? ` · ${formatDateTime(quote.proposedStartAt)}` : ""}
                    </p>
                  ) : null}
                </div>

                {quote.status === "submitted" ? (
                  <Button size="sm" onClick={() => openOfferModal(quote)}>
                    Send quote
                  </Button>
                ) : null}
              </div>
            </li>
          ))}
        </ul>
      )}

      <Modal
        open={selectedQuote !== null}
        onClose={closeOfferModal}
        title="Send a quote"
        description="Propose a price and schedule for this job."
        footer={
          <>
            <Button variant="secondary" onClick={closeOfferModal}>
              Cancel
            </Button>
            <Button
              type="submit"
              form="quote-offer-form"
              loading={submitting}
              loadingLabel="Sending…"
              disabled={activeServices.length === 0}
            >
              Send quote
            </Button>
          </>
        }
      >
        {activeServices.length === 0 ? (
          <Callout tone="warning">Add at least one active service before sending quotes.</Callout>
        ) : (
          <form id="quote-offer-form" onSubmit={(event) => void handleSubmitOffer(event)} className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-2">
              <Input
                label="Amount"
                type="number"
                min="0"
                step="0.01"
                required
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
              />
              <Input
                label="Currency"
                maxLength={3}
                required
                className="uppercase"
                value={currency}
                onChange={(event) => setCurrency(event.target.value)}
              />
            </div>

            <label className="block text-sm font-medium text-foreground">
              Service
              <select
                className="mt-1.5 w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm"
                value={serviceOfferingId}
                onChange={(event) => handleServiceChange(event.target.value)}
                required
              >
                {activeServices.map((service) => (
                  <option key={service.id} value={service.id}>
                    {service.name} ({service.durationMinutes} min)
                  </option>
                ))}
              </select>
            </label>

            <div className="grid gap-4 sm:grid-cols-2">
              <Input
                label="Proposed start"
                type="datetime-local"
                required
                value={proposedStartAt.slice(0, 16)}
                onChange={(event) => {
                  const next = new Date(event.target.value).toISOString();
                  setProposedStartAt(next);
                  const service = activeServices.find((item) => item.id === serviceOfferingId);
                  if (service) {
                    setProposedEndAt(defaultProposedEnd(next, service.durationMinutes));
                  }
                }}
              />
              <Input
                label="Proposed end"
                type="datetime-local"
                required
                value={proposedEndAt.slice(0, 16)}
                onChange={(event) => setProposedEndAt(new Date(event.target.value).toISOString())}
              />
            </div>

            <Textarea
              label="Notes (optional)"
              rows={3}
              value={notes}
              onChange={(event) => setNotes(event.target.value)}
              placeholder="Materials, access instructions, or scope clarifications"
            />
          </form>
        )}
      </Modal>
    </div>
  );
}
