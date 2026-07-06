import Image from "next/image";
import Link from "next/link";
import type { DiscoveryBusinessItem } from "@adeni/shared";
import { getBusinessCoverImage, getCategoryVisual } from "@adeni/shared";
import { Card } from "@/components/ui/card";
import { MapPin } from "lucide-react";

type Props = {
  business: DiscoveryBusinessItem;
};

export function BusinessDiscoveryCard({ business }: Props) {
  const visual = getCategoryVisual(business.categorySlug);
  const imageUrl = getBusinessCoverImage(business.categorySlug);

  return (
    <Link href={`/businesses/${business.slug}`} className="group block">
      <Card interactive padding="sm" className="overflow-hidden p-0 hover:border-accent/40">
        <div className="relative aspect-[4/3] w-full overflow-hidden bg-background">
          <Image
            src={imageUrl}
            alt=""
            fill
            sizes="(max-width: 640px) 100vw, 50vw"
            className="object-cover transition-transform duration-300 group-hover:scale-105"
          />
          <div className="absolute inset-0 bg-gradient-to-t from-black/50 via-transparent to-transparent" />
          <span className="absolute bottom-3 left-3 rounded-full bg-surface/90 px-2.5 py-1 text-xs font-semibold text-foreground backdrop-blur">
            {visual.icon} {visual.label}
          </span>
        </div>
        <div className="p-4">
          <p className="text-lg font-semibold text-foreground">{business.name}</p>
          <p className="mt-1 flex items-center gap-1 text-sm text-muted">
            <MapPin className="h-3.5 w-3.5 shrink-0" aria-hidden />
            {business.area}
          </p>
          <p className="mt-2 text-xs font-semibold text-accent">
            {business.distanceKm.toFixed(1)} km away
          </p>
        </div>
      </Card>
    </Link>
  );
}
