"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { DiscoveryBusinessItem, DiscoveryResponse } from "@adeni/shared";
import { fetchDiscoveryPage } from "@/lib/discovery-fetch";
import { BusinessDiscoveryCard } from "@/components/business-discovery-card";
import { useTranslation } from "@/components/locale-provider";
import { Button } from "@/components/ui/button";
import { LoadingSpinner } from "@/components/ui/loading-spinner";
import { Callout } from "@/components/ui/callout";
import { publicCardGridClass } from "@/lib/layout-classes";

type Props = {
  lat: number;
  lng: number;
  market: string;
  category?: string | null;
  q?: string | null;
  sort?: "distance" | "featured";
  minRating?: number | null;
  initialPage?: DiscoveryResponse;
};

function getNextPageNumber(pages: DiscoveryResponse[]): number {
  const lastPage = pages.at(-1)?.page ?? 0;
  return Number(lastPage) + 1;
}

export function DiscoverBusinessGrid({
  lat,
  lng,
  market,
  category,
  q,
  sort = "distance",
  minRating = null,
  initialPage,
}: Props) {
  const { t } = useTranslation();
  const loadMoreRef = useRef<HTMLDivElement | null>(null);
  const autoLoadPausedRef = useRef(false);
  const queryKey = `${lat},${lng},${market},${category ?? ""},${q ?? ""},${sort},${minRating ?? ""}`;
  const queryKeyRef = useRef(queryKey);

  const [pages, setPages] = useState<DiscoveryResponse[]>(
    initialPage ? [initialPage] : [],
  );
  const [isLoading, setIsLoading] = useState(!initialPage);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [loadMoreError, setLoadMoreError] = useState(false);

  useEffect(() => {
    if (queryKeyRef.current === queryKey) {
      return;
    }

    queryKeyRef.current = queryKey;
    autoLoadPausedRef.current = false;
    setLoadMoreError(false);
    setPages(initialPage ? [initialPage] : []);
    setIsLoading(!initialPage);
  }, [queryKey, initialPage]);

  useEffect(() => {
    if (initialPage) {
      return;
    }

    let cancelled = false;

    const loadFirstPage = async () => {
      setIsLoading(true);
      try {
        const result = await fetchDiscoveryPage({
          lat,
          lng,
          market,
          category,
          q,
          page: 1,
          sort,
          minRating,
        });
        if (!cancelled) {
          setPages([result]);
        }
      } catch {
        if (!cancelled) {
          setPages([]);
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    };

    void loadFirstPage();

    return () => {
      cancelled = true;
    };
  }, [initialPage, lat, lng, market, category, q, sort, minRating]);

  const businesses = pages.flatMap((page) => page.items);
  const totalCount = pages[0]?.totalCount ?? initialPage?.totalCount ?? 0;
  const hasNextPage = businesses.length > 0 && businesses.length < totalCount;

  const loadMore = useCallback(
    async (manual = false) => {
      if (!hasNextPage || isLoadingMore) {
        return;
      }

      if (!manual && autoLoadPausedRef.current) {
        return;
      }

      if (manual) {
        autoLoadPausedRef.current = false;
      }

      const nextPage = getNextPageNumber(pages);
      setLoadMoreError(false);
      setIsLoadingMore(true);

      try {
        const result = await fetchDiscoveryPage({
          lat,
          lng,
          market,
          category,
          q,
          page: nextPage,
          sort,
          minRating,
        });
        setPages((current) => [...current, result]);
      } catch {
        setLoadMoreError(true);
        autoLoadPausedRef.current = true;
      } finally {
        setIsLoadingMore(false);
      }
    },
    [category, hasNextPage, isLoadingMore, lat, lng, market, minRating, pages, q, sort],
  );

  useEffect(() => {
    const target = loadMoreRef.current;
    if (!target || !hasNextPage || autoLoadPausedRef.current) {
      return;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting) {
          void loadMore(false);
        }
      },
      { rootMargin: "240px" },
    );

    observer.observe(target);
    return () => observer.disconnect();
  }, [hasNextPage, loadMore, businesses.length]);

  if (!isLoading && businesses.length === 0 && !initialPage) {
    return (
      <Callout tone="error" className="mt-8">
        {t("discover.gridLoadError")}
      </Callout>
    );
  }

  if (isLoading) {
    return (
      <ul className={`${publicCardGridClass} mt-8`}>
        {Array.from({ length: 8 }).map((_, index) => (
          <li key={index}>
            <div className="h-[320px] animate-pulse rounded-2xl border border-border bg-subtle" />
          </li>
        ))}
      </ul>
    );
  }

  return (
    <div className="mt-8">
      {totalCount > 0 ? (
        <p className="mb-4 text-sm text-muted">
          {t("discover.showingCount", { shown: businesses.length, total: totalCount })}
        </p>
      ) : null}

      <ul className={publicCardGridClass}>
        {businesses.map((business: DiscoveryBusinessItem, index: number) => (
          <li key={`${business.tenantId}-${business.locationId}`}>
            <BusinessDiscoveryCard business={business} imagePriority={index < 4} />
          </li>
        ))}
      </ul>

      <div ref={loadMoreRef} className="flex flex-col items-center gap-3 py-8">
        {isLoadingMore ? (
          <div className="flex items-center gap-2 text-sm text-muted">
            <LoadingSpinner size="sm" label={t("discover.loadingMore")} />
            <span>{t("discover.loadingMore")}</span>
          </div>
        ) : loadMoreError ? (
          <>
            <Callout tone="error" className="max-w-lg">
              {t("discover.loadMoreError")}
            </Callout>
            <Button variant="secondary" size="sm" onClick={() => void loadMore(true)}>
              {t("discover.tryAgain")}
            </Button>
          </>
        ) : hasNextPage ? (
          <Button variant="secondary" size="sm" onClick={() => void loadMore(true)}>
            {t("discover.loadMore")}
          </Button>
        ) : businesses.length > 0 ? (
          <p className="text-sm text-muted">{t("discover.seenAll")}</p>
        ) : null}
      </div>
    </div>
  );
}
