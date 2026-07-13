"use client";

import type { PublicReviewItem } from "@adeni/shared";
import { getReviewCountLabel } from "@adeni/shared";
import { TranslatedText } from "@/components/translated-text";
import { useTranslation } from "@/components/locale-provider";
import { StarRating } from "@/components/star-rating";

type Props = {
  reviews: PublicReviewItem[];
  ratingAvg?: number | null;
  reviewCount?: number | null;
};

export function BusinessReviewsSection({ reviews, ratingAvg, reviewCount }: Props) {
  const { locale, t } = useTranslation();

  if (!reviewCount) {
    return null;
  }

  const reviewLabel = getReviewCountLabel(locale, reviewCount);

  return (
    <section className="mt-8 rounded-2xl border border-border bg-surface p-8 shadow-sm">
      <div className="flex items-center gap-3">
        <h2 className="text-xl font-semibold text-foreground">{t("business.reviewsTitle")}</h2>
        <StarRating rating={ratingAvg ?? 0} size="md" />
        <span className="text-sm text-muted">
          {(ratingAvg ?? 0).toFixed(1)} · {reviewLabel}
        </span>
      </div>

      <ul className="mt-6 space-y-4">
        {reviews.map((review) => (
          <li
            key={review.id}
            className="border-t border-border pt-4 first:border-t-0 first:pt-0"
          >
            <div className="flex items-center justify-between gap-3">
              <p className="font-medium text-foreground">{review.customerDisplayName}</p>
              <StarRating rating={review.rating} />
            </div>
            {review.comment ? (
              <TranslatedText
                as="p"
                text={review.comment}
                showBadge
                className="mt-2 text-sm text-muted"
              />
            ) : null}
            <p className="mt-2 text-xs text-muted-foreground">
              {new Date(review.createdAt).toLocaleDateString(locale)}
            </p>
          </li>
        ))}
      </ul>
    </section>
  );
}
