"use client";

import { useState } from "react";
import { discoveryCtaLabel } from "@adeni/shared";
import { Callout } from "@/components/ui/callout";
import { Input, Textarea } from "@/components/ui/input";
import { useActionLoading } from "@/contexts/action-loading-context";

type Props = {
  slug: string;
  loginHref: string;
  enabled: boolean;
};

export function QuoteRequestPanel({ slug, loginHref, enabled }: Props) {
  const { run } = useActionLoading();
  const [description, setDescription] = useState("");
  const [serviceAddress, setServiceAddress] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const title = discoveryCtaLabel("get_quote");

  if (!enabled) {
    return (
      <section className="mt-8 rounded-2xl border border-border bg-surface p-8 shadow-sm">
        <h2 className="text-lg font-semibold">{title}</h2>
        <p className="mt-2 text-sm text-muted">
          Sign in to describe your job and request a quote from this business.
        </p>
        <a href={loginHref} className="mt-4 inline-flex rounded-full bg-primary px-6 py-3 text-sm font-semibold text-primary-foreground">
          Sign in to continue
        </a>
      </section>
    );
  }

  async function handleSubmit() {
    setSubmitting(true);
    setError(null);

    try {
      await run("Sending quote request…", async () => {
        const response = await fetch(`/api/businesses/${encodeURIComponent(slug)}/quote-requests`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            description: description.trim(),
            serviceAddress: serviceAddress.trim() || undefined,
          }),
        });

        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(typeof payload.title === "string" ? payload.title : "Could not send quote request.");
        }

        setSubmitted(true);
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not send quote request.");
    } finally {
      setSubmitting(false);
    }
  }

  if (submitted) {
    return (
      <section className="mt-8 rounded-2xl border border-accent/30 bg-surface p-8 shadow-sm">
        <p className="text-sm font-semibold uppercase tracking-widest text-accent">Quote request sent</p>
        <h2 className="mt-2 text-xl font-bold">We received your job details</h2>
        <p className="mt-3 text-sm text-muted">
          The business will review your request and follow up with a quote. Full quote workflows arrive in a later
          release.
        </p>
      </section>
    );
  }

  return (
    <section className="mt-8 rounded-2xl border border-border bg-surface p-8 shadow-sm">
      <h2 className="text-lg font-semibold">{title}</h2>
      <p className="mt-2 text-sm text-muted">
        Describe the job and where service is needed. Photo upload and formal quotes are coming soon.
      </p>

      <div className="mt-6 space-y-4">
        <Textarea
          label="Describe the job"
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          rows={5}
          placeholder="Example: Kitchen sink is leaking under the cabinet. Need repair this week."
        />
        <Input
          label="Service address (optional)"
          value={serviceAddress}
          onChange={(event) => setServiceAddress(event.target.value)}
          placeholder="Street, area, city"
        />
        {error ? <Callout tone="error">{error}</Callout> : null}
        <button
          type="button"
          onClick={handleSubmit}
          disabled={submitting || description.trim().length < 10}
          className="inline-flex rounded-full bg-primary px-6 py-3 text-sm font-semibold text-primary-foreground disabled:opacity-60"
        >
          {submitting ? "Sending…" : "Request quote"}
        </button>
      </div>
    </section>
  );
}
