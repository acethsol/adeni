"use client";

import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { LoadingSpinner } from "@/components/ui/loading-spinner";

export function StubCheckoutClient() {
  const params = useParams<{ reference: string }>();
  const router = useRouter();
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handlePay(success: boolean) {
    setSubmitting(true);
    setError(null);

    try {
      if (success) {
        const response = await fetch("/api/payments/stub/confirm", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ reference: params.reference }),
        });
        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(payload.title ?? "Payment confirmation failed.");
        }
        router.push(`/checkout/receipt?id=${encodeURIComponent(payload.id)}`);
        return;
      }

      setError("Payment was declined in stub mode.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Payment failed.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="mx-auto flex min-h-screen max-w-lg flex-col justify-center px-6 py-16">
      <p className="text-sm font-semibold uppercase tracking-widest text-[#40916c]">Stub checkout</p>
      <h1 className="mt-2 text-2xl font-bold">Complete test payment</h1>
      <p className="mt-2 text-sm text-[#1b4332]/70">
        Reference: <code className="text-xs">{params.reference}</code>
      </p>
      <p className="mt-4 text-sm text-[#1b4332]/80">
        This page simulates Paystack checkout in local development. Choose an outcome below.
      </p>
      {error ? (
        <p className="mt-4 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-800">{error}</p>
      ) : null}
      <div className="mt-8 flex flex-col gap-3 sm:flex-row">
        <button
          type="button"
          disabled={submitting}
          onClick={() => void handlePay(true)}
          className="inline-flex flex-1 items-center justify-center gap-2 rounded-full bg-[#1b4332] px-6 py-3 text-sm font-semibold text-white disabled:opacity-60"
        >
          {submitting ? <LoadingSpinner size="sm" label="Processing" className="border-white/30 border-t-white" /> : null}
          Pay successfully
        </button>
        <button
          type="button"
          disabled={submitting}
          onClick={() => void handlePay(false)}
          className="inline-flex flex-1 items-center justify-center rounded-full border border-[#1b4332]/20 px-6 py-3 text-sm font-semibold text-[#1b4332] disabled:opacity-60"
        >
          Decline
        </button>
      </div>
    </main>
  );
}
