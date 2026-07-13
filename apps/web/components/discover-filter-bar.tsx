"use client";

import { useRouter } from "next/navigation";
import { X } from "lucide-react";
import { useTranslation } from "@/components/locale-provider";
import { cn } from "@/lib/cn";

type Props = {
  category: string | null;
  searchQuery: string | null;
  sort: "distance" | "featured";
  minRating: number | null;
  className?: string;
};

const RATING_OPTIONS = [4, 3, 2, 1];

function buildHref(params: {
  category: string | null;
  q: string | null;
  sort: "distance" | "featured";
  minRating: number | null;
}) {
  const query = new URLSearchParams();
  if (params.category) {
    query.set("category", params.category);
  }
  if (params.q) {
    query.set("q", params.q);
  }
  if (params.sort !== "distance") {
    query.set("sort", params.sort);
  }
  if (params.minRating) {
    query.set("minRating", String(params.minRating));
  }
  const search = query.toString();
  return search ? `/discover?${search}` : "/discover";
}

const selectClass =
  "rounded-full border border-border-strong bg-surface px-3.5 py-2 text-sm font-medium text-foreground outline-none transition-shadow focus:border-accent focus:ring-2 focus:ring-accent/20";

export function DiscoverFilterBar({ category, searchQuery, sort, minRating, className }: Props) {
  const router = useRouter();
  const { t } = useTranslation();

  const hasActiveFilters = sort !== "distance" || Boolean(minRating);

  function navigate(next: Partial<{ sort: "distance" | "featured"; minRating: number | null }>) {
    router.push(
      buildHref({
        category,
        q: searchQuery,
        sort: next.sort ?? sort,
        minRating: next.minRating === undefined ? minRating : next.minRating,
      }),
    );
  }

  return (
    <div className={cn("flex flex-wrap items-center gap-3", className)}>
      <label className="flex items-center gap-2 text-sm text-muted">
        <span className="font-medium text-foreground">{t("discover.sortLabel")}</span>
        <select
          value={sort}
          onChange={(event) => navigate({ sort: event.target.value as "distance" | "featured" })}
          className={selectClass}
        >
          <option value="distance">{t("discover.sortDistance")}</option>
          <option value="featured">{t("discover.sortFeatured")}</option>
        </select>
      </label>

      <label className="flex items-center gap-2 text-sm text-muted">
        <span className="font-medium text-foreground">{t("discover.minRatingLabel")}</span>
        <select
          value={minRating ?? ""}
          onChange={(event) =>
            navigate({ minRating: event.target.value ? Number(event.target.value) : null })
          }
          className={selectClass}
        >
          <option value="">{t("discover.minRatingAny")}</option>
          {RATING_OPTIONS.map((value) => (
            <option key={value} value={value}>
              {t("discover.minRatingValue", { value })}
            </option>
          ))}
        </select>
      </label>

      {hasActiveFilters ? (
        <button
          type="button"
          onClick={() => router.push(buildHref({ category, q: searchQuery, sort: "distance", minRating: null }))}
          className="inline-flex items-center gap-1.5 rounded-full px-3.5 py-2 text-sm font-semibold text-accent transition-colors hover:bg-accent/10"
        >
          <X className="h-3.5 w-3.5" aria-hidden />
          {t("discover.clearFilters")}
        </button>
      ) : null}
    </div>
  );
}
