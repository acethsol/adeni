"use client";

import Link from "next/link";
import { ShieldCheck } from "lucide-react";
import type { DiscoveryBusinessItem } from "@adeni/shared";
import {
  getBusinessCoverImage,
  getCategoryLabel,
  getReviewCountLabel,
  resolveBusinessCoverImage,
} from "@adeni/shared";
import { useTranslation } from "@/components/locale-provider";
import { NewReviewsBadge } from "@/components/new-reviews-badge";
import { StarRating } from "@/components/star-rating";
import { Badge } from "@/components/ui/badge";
import {
  MediaCard,
  MediaCardActions,
  MediaCardBody,
  MediaCardMeta,
  MediaCardTitle,
} from "@/components/ui/media-card";
import { RemoteImage } from "@/components/ui/remote-image";
import { cn } from "@/lib/cn";

type Props = {
  business: DiscoveryBusinessItem;
  className?: string;
  imagePriority?: boolean;
};

export function BusinessDiscoveryCard({ business, className, imagePriority = false }: Props) {
  const { locale, t } = useTranslation();
  const imageUrl = resolveBusinessCoverImage(business.categorySlug, business.coverImageUrl);
  const fallbackImageUrl = getBusinessCoverImage(business.categorySlug);
  const categoryLabel = getCategoryLabel(locale, business.categorySlug);
  const hasReviews = Boolean(business.reviewCount && business.reviewCount > 0);
  const reviewLabel = hasReviews ? getReviewCountLabel(locale, business.reviewCount) : null;

  return (
    <Link href={`/businesses/${business.slug}`} className={cn("block h-full", className)}>
      <MediaCard>
        <div className="relative aspect-[5/4] w-full overflow-hidden bg-muted">
          <RemoteImage
            src={imageUrl}
            fallbackSrc={fallbackImageUrl}
            alt=""
            fill
            sizes="280px"
            priority={imagePriority}
            className="object-cover"
          />
          <div className="absolute left-3 top-3 z-10">
            <span className="inline-flex items-center gap-1 rounded-full border border-accent/20 bg-surface/95 px-2.5 py-1 text-xs font-semibold text-accent shadow-sm backdrop-blur-sm">
              <ShieldCheck className="h-3.5 w-3.5" aria-hidden />
              {t("business.verified")}
            </span>
          </div>
          {!hasReviews ? (
            <div className="absolute right-3 top-3 z-10">
              <NewReviewsBadge
                badgeLabel={t("business.newBadge")}
                tooltipLabel={t("business.awaitingReviews")}
              />
            </div>
          ) : null}
        </div>

        <MediaCardBody>
          <MediaCardTitle>{business.name}</MediaCardTitle>

          <div className="flex min-h-5 items-center gap-2 text-sm">
            <StarRating
              rating={hasReviews ? (business.ratingAvg ?? 0) : 0}
              tone="accent"
            />
            {hasReviews ? (
              <span className="font-medium text-foreground">
                {(business.ratingAvg ?? 0).toFixed(1)} · {reviewLabel}
              </span>
            ) : null}
          </div>

          <MediaCardMeta>
            {categoryLabel} · {business.area} · {business.distanceKm.toFixed(1)} km
          </MediaCardMeta>

          <MediaCardActions>
            <Badge tone="accent">{t("business.viewProfile")}</Badge>
            <Badge tone="accent">{t("business.bookNow")}</Badge>
          </MediaCardActions>
        </MediaCardBody>
      </MediaCard>
    </Link>
  );
}
