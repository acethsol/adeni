import Link from "next/link";
import type { Category } from "@adeni/shared";
import { getCategoryVisual, getDefaultBusinessCoverImage } from "@adeni/shared";
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
  const visual = getCategoryVisual(category.slug, category.name);
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
            {category.name}
          </MediaCardTitle>
          <MediaCardMeta>Browse in {category.name.toLowerCase()}</MediaCardMeta>
        </MediaCardBody>
      </MediaCard>
    </Link>
  );
}
