"use client";

import { useState } from "react";
import type { CustomerBookingResponse } from "@adeni/shared";
import { useTranslation } from "@/components/locale-provider";
import { StarRating } from "@/components/star-rating";
import { Button } from "@/components/ui/button";
import { useActionLoading } from "@/contexts/action-loading-context";
import { useToast } from "@/contexts/toast-context";

type Props = {
  booking: CustomerBookingResponse;
  onSubmitted: () => void;
};

export function BookingReviewForm({ booking, onSubmitted }: Props) {
  const { t } = useTranslation();
  const { run } = useActionLoading();
  const toast = useToast();
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      await run(t("bookings.review.submitting"), async () => {
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
            typeof payload.title === "string" ? payload.title : t("bookings.review.submitError"),
          );
        }

        onSubmitted();
      });
      toast.success(t("bookings.review.submit"));
    } catch (err) {
      const messageText = err instanceof Error ? err.message : t("bookings.review.submitError");
      setError(messageText);
      toast.error(messageText);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form
      onSubmit={(event) => void handleSubmit(event)}
      className="mt-4 space-y-3 rounded-xl border border-border bg-subtle p-4"
    >
      <p className="text-sm font-medium text-foreground">{t("bookings.review.rateVisit")}</p>
      <div className="flex gap-2">
        {[1, 2, 3, 4, 5].map((value) => (
          <button
            key={value}
            type="button"
            onClick={() => setRating(value)}
            className={`rounded-full px-3 py-1 text-sm ${
              rating >= value
                ? "bg-primary text-primary-foreground"
                : "border border-border-strong text-foreground"
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
        placeholder={t("bookings.review.commentPlaceholder")}
        className="w-full rounded-lg border border-border-strong px-3 py-2 text-sm"
      />
      {error ? <p className="text-sm text-destructive">{error}</p> : null}
      <Button type="submit" loading={submitting} loadingLabel={t("bookings.review.submitting")}>
        {t("bookings.review.submit")}
      </Button>
    </form>
  );
}

export function BookingReviewSummary({ rating }: { rating: number }) {
  const { t } = useTranslation();

  return (
    <div className="mt-3 flex items-center gap-2 text-sm text-muted">
      <StarRating rating={rating} />
      <span>{t("bookings.review.youRated", { rating })}</span>
    </div>
  );
}
