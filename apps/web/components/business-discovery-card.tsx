import Image from "next/image";
import Link from "next/link";
import type { DiscoveryBusinessItem } from "@adeni/shared";
import { formatCategoryLabel, formatRatingSummary, resolveBusinessCoverImage } from "@adeni/shared";
import { StarRating } from "@/components/star-rating";
import { MapPin } from "lucide-react";

type Props = {
  business: DiscoveryBusinessItem;
};

export function BusinessDiscoveryCard({ business }: Props) {
  const imageUrl = resolveBusinessCoverImage(business.categorySlug, business.coverImageUrl);
  const categoryLabel = formatCategoryLabel(business.categorySlug);

  return (
    <Link href={`/businesses/${business.slug}`} className="group block">
      <div className="relative aspect-[4/3] w-full overflow-hidden rounded-xl bg-muted">
        <Image
          src={imageUrl}
          alt=""
          fill
          sizes="(max-width: 640px) 100vw, 50vw"
          className="object-cover"
        />
      </div>
      <div className="mt-3 space-y-1">
        <div className="flex items-start justify-between gap-3">
          <p className="font-semibold leading-snug text-foreground">{business.name}</p>
          <span className="shrink-0 text-xs font-medium text-muted">Verified</span>
        </div>
        <p className="text-sm text-muted">{categoryLabel}</p>
        <p className="flex items-center gap-2 text-sm text-muted">
          {business.reviewCount ? (
            <>
              <StarRating rating={business.ratingAvg ?? 0} />
              <span>{formatRatingSummary(business.ratingAvg, business.reviewCount)}</span>
            </>
          ) : (
            <span>New</span>
          )}
        </p>
        <p className="flex items-center gap-1 text-sm text-muted">
          <MapPin className="h-3.5 w-3.5 shrink-0" aria-hidden />
          {business.area} · {business.distanceKm.toFixed(1)} km
        </p>
      </div>
    </Link>
  );
}
