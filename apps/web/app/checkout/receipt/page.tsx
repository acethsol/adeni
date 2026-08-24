"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import type { PaymentIntentResponse } from "@adeni/shared";
import { LoadingPanel } from "@/components/loading-panel";

function formatMoney(amount: number, currency: string) {
  try {
    return new Intl.NumberFormat(undefined, { style: "currency", currency }).format(amount);
  } catch {
    return `${currency} ${amount.toFixed(2)}`;
  }
}

export default function PaymentReceiptPage() {
  const searchParams = useSearchParams();
  const paymentId = searchParams.get("id");
  const [payment, setPayment] = useState<PaymentIntentResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!paymentId) {
      setError("Missing payment reference.");
      return;
    }

    void (async () => {
      try {
        const response = await fetch(`/api/payments/${encodeURIComponent(paymentId)}`);
        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(payload.title ?? "Could not load receipt.");
        }
        setPayment(payload as PaymentIntentResponse);
      } catch (err) {
        setError(err instanceof Error ? err.message : "Could not load receipt.");
      }
    })();
  }, [paymentId]);

  if (error) {
    return (
      <main className="mx-auto max-w-lg px-6 py-16">
        <p className="text-sm text-red-700">{error}</p>
      </main>
    );
  }

  if (!payment) {
    return (
      <main className="mx-auto max-w-lg px-6 py-16">
        <LoadingPanel message="Loading receipt…" variant="card" />
      </main>
    );
  }

  const succeeded = payment.status === "completed";

  return (
    <main className="mx-auto max-w-lg px-6 py-16">
      <section className="rounded-2xl border border-[#40916c]/30 bg-white p-8 shadow-sm">
        <p className={`text-sm font-semibold uppercase tracking-widest ${succeeded ? "text-[#40916c]" : "text-amber-700"}`}>
          {succeeded ? "Payment successful" : `Payment ${payment.status}`}
        </p>
        <h1 className="mt-2 text-2xl font-bold">{formatMoney(payment.amount, payment.currency)}</h1>
        {payment.description ? (
          <p className="mt-2 text-sm text-[#1b4332]/70">{payment.description}</p>
        ) : null}
        <dl className="mt-6 space-y-2 text-sm">
          <div className="flex justify-between gap-4">
            <dt className="text-[#1b4332]/60">Reference</dt>
            <dd className="font-mono text-xs">{payment.providerReference}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-[#1b4332]/60">Type</dt>
            <dd className="capitalize">{payment.type}</dd>
          </div>
        </dl>
        <Link
          href="/"
          className="mt-8 inline-block rounded-full bg-[#1b4332] px-6 py-3 text-sm font-semibold text-white"
        >
          Done
        </Link>
      </section>
    </main>
  );
}
