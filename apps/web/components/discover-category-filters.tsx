"use client";

import Link from "next/link";
import type { Category } from "@adeni/shared";
import { getCategoryLabel } from "@adeni/shared";
import { useTranslation } from "@/components/locale-provider";
import { cn } from "@/lib/cn";

type Props = {
  categories: Category[];
  selectedCategory: string | null;
  searchQuery: string | null;
  className?: string;
};

function buildDiscoverHref(category: string | null, searchQuery: string | null) {
  const params = new URLSearchParams();
  if (category) {
    params.set("category", category);
  }
  if (searchQuery) {
    params.set("q", searchQuery);
  }
  const query = params.toString();
  return query ? `/discover?${query}` : "/discover";
}

function filterChipClass(active: boolean) {
  return cn(
    "rounded-full px-4 py-2 text-sm font-semibold transition-colors",
    active
      ? "bg-primary text-primary-foreground shadow-sm"
      : "border border-border-strong bg-surface text-foreground hover:border-accent/40",
  );
}

export function CategoryFilterLinks({
  categories,
  selectedCategory,
  searchQuery,
  className,
}: Props) {
  const { locale, t } = useTranslation();

  return (
    <div className={cn("flex flex-wrap gap-2", className)}>
      <Link href={buildDiscoverHref(null, searchQuery)} className={filterChipClass(!selectedCategory)}>
        {t("categories.all")}
      </Link>
      {categories.map((item) => (
        <Link
          key={item.id}
          href={buildDiscoverHref(item.slug, searchQuery)}
          className={filterChipClass(selectedCategory === item.slug)}
        >
          {getCategoryLabel(locale, item.slug, item.name)}
        </Link>
      ))}
    </div>
  );
}
