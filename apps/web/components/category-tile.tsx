import Image from "next/image";
import Link from "next/link";
import type { Category } from "@adeni/shared";
import { getCategoryVisual } from "@adeni/shared";

type Props = {
  category: Category;
};

export function CategoryTile({ category }: Props) {
  const visual = getCategoryVisual(category.slug, category.name);

  return (
    <Link href={`/discover?category=${category.slug}`} className="group block">
      <div className="relative aspect-[5/3] w-full overflow-hidden rounded-xl bg-muted">
        <Image
          src={visual.imageUrl}
          alt=""
          fill
          sizes="(max-width: 640px) 100vw, 33vw"
          className="object-cover"
        />
      </div>
      <p className="mt-2 text-sm font-semibold text-foreground">
        <span aria-hidden>{visual.icon} </span>
        {category.name}
      </p>
    </Link>
  );
}
