import Image from "next/image";
import Link from "next/link";
import type { Category } from "@adeni/shared";
import { getCategoryVisual } from "@adeni/shared";
import { Card } from "@/components/ui/card";

type Props = {
  category: Category;
};

export function CategoryTile({ category }: Props) {
  const visual = getCategoryVisual(category.slug, category.name);

  return (
    <Link href={`/discover?category=${category.slug}`} className="group block">
      <Card interactive padding="sm" className="overflow-hidden p-0 hover:border-accent/40">
        <div className="relative aspect-[5/3] w-full overflow-hidden">
          <Image
            src={visual.imageUrl}
            alt=""
            fill
            sizes="(max-width: 640px) 100vw, 33vw"
            className="object-cover transition-transform duration-300 group-hover:scale-105"
          />
          <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-black/10 to-transparent" />
          <div className="absolute bottom-3 left-3 right-3">
            <span className="text-lg" aria-hidden>
              {visual.icon}
            </span>
            <p className="mt-1 text-base font-semibold text-white">{category.name}</p>
          </div>
        </div>
      </Card>
    </Link>
  );
}
