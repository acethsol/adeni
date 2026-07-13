"use client";

import Link from "next/link";
import type { Category } from "@adeni/shared";
import { getCategoryLabel, getCategoryVisual, getDefaultBusinessCoverImage } from "@adeni/shared";
import { useTranslation } from "@/components/locale-provider";
import {
  MediaCard,
  MediaCardBody,
  MediaCardImage,
  MediaCardMeta,
  MediaCardTitle,
} from "@/components/ui/media-card";

type Props = {
  category: Category;
};

export function CategoryTile({ category }: Props) {
  const { locale, t } = useTranslation();
  const label = getCategoryLabel(locale, category.slug, category.name);
  const visual = getCategoryVisual(category.slug, label);
  const fallbackImageUrl = getDefaultBusinessCoverImage();

  return (
    <Link href={`/discover?category=${category.slug}`} className="block h-full">
      <MediaCard>
        <MediaCardImage
          src={visual.imageUrl}
          fallbackSrc={fallbackImageUrl}
          sizes="(max-width: 640px) 85vw, 280px"
        />
        <MediaCardBody>
          <MediaCardTitle>
            <span aria-hidden>{visual.icon} </span>
            {label}
          </MediaCardTitle>
          <MediaCardMeta>
            {t("categories.browseIn", { category: label.toLowerCase() })}
          </MediaCardMeta>
        </MediaCardBody>
      </MediaCard>
    </Link>
  );
}
