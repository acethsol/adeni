"use client";

import { useState } from "react";
import type { CustomerBookingResponse } from "@adeni/shared";
import { StarRating } from "@/components/star-rating";
import { Button } from "@/components/ui/button";

type Props = {
  booking: CustomerBookingResponse;
  onSubmitted: () => void;
};

export function BookingReviewForm({ booking, onSubmitted }: Props) {
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      const response = await fetch(`/api/bookings/${encodeURIComponent(booking.id)}/review`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          rating,
          comment: comment.trim() || undefined,
        }),
      });

      const payload = await response.json().catch(() => ({}));
      if (!response.ok) {
        throw new Error(
          typeof payload.title === "string" ? payload.title : "Could not submit review.",
        );
      }

      onSubmitted();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not submit review.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={(event) => void handleSubmit(event)} className="mt-4 space-y-3 rounded-xl border border-[#1b4332]/10 bg-[#f6f8f6] p-4">
      <p className="text-sm font-medium text-[#1b4332]">Rate your visit</p>
      <div className="flex gap-2">
        {[1, 2, 3, 4, 5].map((value) => (
          <button
            key={value}
            type="button"
            onClick={() => setRating(value)}
            className={`rounded-full px-3 py-1 text-sm ${
              rating >= value
                ? "bg-[#1b4332] text-white"
                : "border border-[#1b4332]/20 text-[#1b4332]"
            }`}
          >
            {value}★
          </button>
        ))}
      </div>
      <textarea
        value={comment}
        onChange={(event) => setComment(event.target.value)}
        rows={3}
        placeholder="Share what went well (optional)"
        className="w-full rounded-lg border border-[#1b4332]/20 px-3 py-2 text-sm"
      />
      {error ? <p className="text-sm text-red-700">{error}</p> : null}
      <Button type="submit" disabled={submitting}>
        {submitting ? "Submitting…" : "Submit review"}
      </Button>
    </form>
  );
}

export function BookingReviewSummary({ rating }: { rating: number }) {
  return (
    <div className="mt-3 flex items-center gap-2 text-sm text-[#1b4332]/80">
      <StarRating rating={rating} />
      <span>You rated this visit {rating}/5</span>
    </div>
  );
}
