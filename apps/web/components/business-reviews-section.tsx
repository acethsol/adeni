import type { PublicReviewItem } from "@adeni/shared";
import { StarRating } from "@/components/star-rating";

type Props = {
  reviews: PublicReviewItem[];
  ratingAvg?: number | null;
  reviewCount?: number | null;
};

export function BusinessReviewsSection({ reviews, ratingAvg, reviewCount }: Props) {
  if (!reviewCount) {
    return null;
  }

  return (
    <section className="mt-8 rounded-2xl border border-[#1b4332]/10 bg-white p-8 shadow-sm">
      <div className="flex items-center gap-3">
        <h2 className="text-xl font-semibold text-[#1b4332]">Reviews</h2>
        <StarRating rating={ratingAvg ?? 0} size="md" />
        <span className="text-sm text-[#1b4332]/70">
          {(ratingAvg ?? 0).toFixed(1)} · {reviewCount} review{reviewCount === 1 ? "" : "s"}
        </span>
      </div>

      <ul className="mt-6 space-y-4">
        {reviews.map((review) => (
          <li key={review.id} className="border-t border-[#1b4332]/10 pt-4 first:border-t-0 first:pt-0">
            <div className="flex items-center justify-between gap-3">
              <p className="font-medium text-[#1b4332]">{review.customerDisplayName}</p>
              <StarRating rating={review.rating} />
            </div>
            {review.comment ? (
              <p className="mt-2 text-sm text-[#1b4332]/80">{review.comment}</p>
            ) : null}
            <p className="mt-2 text-xs text-[#1b4332]/50">
              {new Date(review.createdAt).toLocaleDateString()}
            </p>
          </li>
        ))}
      </ul>
    </section>
  );
}
