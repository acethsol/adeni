"use client";

import { useCallback, useEffect, useState } from "react";
import { MessageSquareText, Star } from "lucide-react";
import type { PublicReviewItem } from "@adeni/shared";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { EmptyState } from "@/components/ui/empty-state";
import { SkeletonList } from "@/components/ui/skeleton";
import { cn } from "@/lib/cn";

type ReviewsPayload = {
  ratingAvg: number | null;
  reviewCount: number;
  locationName?: string;
  items: PublicReviewItem[];
  page: number;
  pageSize: number;
  totalCount: number;
};

const PAGE_SIZE = 5;

function StarRow({ rating, size = "sm" }: { rating: number; size?: "sm" | "lg" }) {
  const starClass = size === "lg" ? "h-5 w-5" : "h-3.5 w-3.5";
  return (
    <div className="flex items-center gap-0.5" aria-label={`${rating} out of 5 stars`}>
      {Array.from({ length: 5 }, (_, index) => (
        <Star
          key={index}
          className={cn(starClass, index < Math.round(rating) ? "fill-amber-400 text-amber-400" : "text-border-strong")}
          aria-hidden
        />
      ))}
    </div>
  );
}

function formatDate(value: string): string {
  try {
    return new Date(value).toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
  } catch {
    return value;
  }
}

export function BusinessReviewsPanel() {
  const [data, setData] = useState<ReviewsPayload | null>(null);
  const [items, setItems] = useState<PublicReviewItem[]>([]);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (nextPage: number, append: boolean) => {
    if (append) {
      setLoadingMore(true);
    } else {
      setLoading(true);
    }
    setError(null);

    try {
      const response = await fetch(`/api/business/reviews?page=${nextPage}&pageSize=${PAGE_SIZE}`);
      if (!response.ok) {
        throw new Error("Could not load reviews.");
      }
      const payload = (await response.json()) as ReviewsPayload;
      setData(payload);
      setItems((current) => (append ? [...current, ...payload.items] : payload.items));
      setPage(nextPage);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load reviews.");
    } finally {
      setLoading(false);
      setLoadingMore(false);
    }
  }, []);

  useEffect(() => {
    void load(1, false);
  }, [load]);

  if (loading) {
    return <SkeletonList count={3} />;
  }

  if (error) {
    return <Callout tone="error">{error}</Callout>;
  }

  const hasMore = data ? items.length < data.totalCount : false;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center gap-4 rounded-xl border border-border bg-subtle/40 p-4">
        <p className="text-3xl font-bold tracking-tight text-foreground">
          {data?.ratingAvg ? data.ratingAvg.toFixed(1) : "—"}
        </p>
        <div>
          <StarRow rating={data?.ratingAvg ?? 0} size="lg" />
          <p className="mt-1 text-sm text-muted">
            {data?.reviewCount
              ? `${data.reviewCount.toLocaleString()} review${data.reviewCount === 1 ? "" : "s"}`
              : "No reviews yet"}
            {data?.locationName ? ` · ${data.locationName}` : null}
          </p>
        </div>
      </div>

      {items.length === 0 ? (
        <EmptyState
          icon={<MessageSquareText className="h-6 w-6" aria-hidden />}
          title="No reviews yet"
          description="Customer reviews will show up here once they start booking and rating your service."
        />
      ) : (
        <>
          <ul className="space-y-3">
            {items.map((review) => (
              <li key={review.id} className="rounded-xl border border-border bg-surface p-4 shadow-sm">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <p className="font-semibold text-foreground">{review.customerDisplayName}</p>
                  <div className="flex items-center gap-2">
                    <StarRow rating={review.rating} />
                    <span className="text-xs text-muted-foreground">{formatDate(review.createdAt)}</span>
                  </div>
                </div>
                {review.comment ? (
                  <p className="mt-2 text-sm leading-relaxed text-muted">{review.comment}</p>
                ) : null}
              </li>
            ))}
          </ul>

          {hasMore ? (
            <div className="flex justify-center">
              <Button
                variant="secondary"
                size="sm"
                onClick={() => void load(page + 1, true)}
                loading={loadingMore}
                loadingLabel="Loading…"
              >
                Load more reviews
              </Button>
            </div>
          ) : null}
        </>
      )}
    </div>
  );
}
