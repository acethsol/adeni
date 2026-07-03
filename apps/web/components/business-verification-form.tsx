"use client";

import { useState } from "react";
import { VERIFICATION_DOCUMENT_LABELS } from "@adeni/shared";

type Props = {
  canSubmit: boolean;
};

export function BusinessVerificationForm({ canSubmit }: Props) {
  const [documentType, setDocumentType] = useState(0);
  const [referenceNumber, setReferenceNumber] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (!canSubmit) {
    return null;
  }

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setMessage(null);
    setError(null);

    try {
      const response = await fetch("/api/business/verification", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          documents: [{ documentType, referenceNumber: referenceNumber.trim() }],
        }),
      });

      if (!response.ok) {
        const payload = await response.json().catch(() => ({}));
        throw new Error(
          typeof payload.title === "string"
            ? payload.title
            : "Could not submit verification.",
        );
      }

      setMessage("Verification submitted. An admin will review your business.");
      setReferenceNumber("");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not submit verification.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form
      onSubmit={(event) => void handleSubmit(event)}
      className="mt-6 rounded-xl border border-[#1b4332]/10 bg-white p-6 shadow-sm"
    >
      <h2 className="text-lg font-semibold">Submit verification</h2>
      <p className="mt-2 text-sm text-[#1b4332]/70">
        Provide a registration or ID reference for admin review.
      </p>

      {message ? (
        <p className="mt-4 rounded-lg bg-green-50 px-4 py-3 text-sm text-green-800">{message}</p>
      ) : null}
      {error ? (
        <p className="mt-4 rounded-lg bg-red-50 px-4 py-3 text-sm text-red-800">{error}</p>
      ) : null}

      <div className="mt-4 space-y-4">
        <label className="block">
          <span className="text-sm font-medium text-[#1b4332]/70">Document type</span>
          <select
            value={documentType}
            onChange={(event) => setDocumentType(Number(event.target.value))}
            className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
          >
            {Object.entries(VERIFICATION_DOCUMENT_LABELS).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>

        <label className="block">
          <span className="text-sm font-medium text-[#1b4332]/70">Reference number</span>
          <input
            required
            value={referenceNumber}
            onChange={(event) => setReferenceNumber(event.target.value)}
            placeholder="e.g. RC123456"
            className="mt-1 w-full rounded-lg border border-[#1b4332]/20 px-3 py-2"
          />
        </label>
      </div>

      <button
        type="submit"
        disabled={submitting}
        className="mt-4 rounded-full bg-[#1b4332] px-5 py-2.5 text-sm font-medium text-white disabled:opacity-60"
      >
        {submitting ? "Submitting…" : "Submit for verification"}
      </button>
    </form>
  );
}
