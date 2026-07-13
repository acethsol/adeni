import type { Metadata } from "next";
import { Callout } from "@/components/ui/callout";
import { EmptyState } from "@/components/ui/empty-state";
import { PageHeader } from "@/components/ui/page-header";
import { PublicHeader } from "@/components/public-header";
import { HERO_SEARCH_ANCHOR_ID } from "@/lib/hero-search";
import { canAccessMyBookings } from "@/lib/customer-access";
import { HeroDiscoverySearch } from "@/components/hero-discovery-search";
import { CategoryFilterLinks } from "@/components/discover-category-filters";
import { DiscoverFilterBar } from "@/components/discover-filter-bar";
import { DiscoverBusinessGrid } from "@/components/discover-business-grid";
import type { Category, DiscoveryResponse } from "@adeni/shared";
import { DISCOVERY_PAGE_SIZE, getCategoryLabel, parseSearchIntent, t } from "@adeni/shared";
import { createApiClient } from "@/lib/adeni";
import { publicContainerClass } from "@/lib/layout-classes";
import { getActiveMarketConfig, getDiscoveryLocation } from "@/lib/market";
import { getLocale } from "@/lib/locale";

export const revalidate = 120;

export async function generateMetadata(): Promise<Metadata> {
  const [market, locale] = await Promise.all([getActiveMarketConfig(), getLocale()]);

  return {
    title: t(locale, "discover.metaTitle", { market: market.name }),
    description: t(locale, "discover.metaDescription", { market: market.name }),
  };
}

type Props = {
  searchParams: Promise<{ category?: string; q?: string; sort?: string; minRating?: string }>;
};

function parseSort(value: string | undefined): "distance" | "featured" {
  return value === "featured" ? "featured" : "distance";
}

function parseMinRating(value: string | undefined): number | null {
  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed >= 1 && parsed <= 5 ? Math.round(parsed) : null;
}

async function getCategories(): Promise<Category[]> {
  try {
    const client = createApiClient();
    return await client.getCategories();
  } catch {
    return [];
  }
}

export default async function DiscoverPage({ searchParams }: Props) {
  const [market, locale] = await Promise.all([getActiveMarketConfig(), getLocale()]);
  const searchLocation = await getDiscoveryLocation();
  const { category, q, sort: sortParam, minRating: minRatingParam } = await searchParams;
  const categories = await getCategories();
  const selectedCategory = category?.trim().toLowerCase() || null;
  const searchQuery = q?.trim() || null;
  const sort = parseSort(sortParam);
  const minRating = parseMinRating(minRatingParam);

  let loadError: string | null = null;
  let initialPage: DiscoveryResponse | null = null;

  try {
    const client = createApiClient();
    const result = await client.searchDiscovery({
      lat: searchLocation.lat,
      lng: searchLocation.lng,
      market: market.id,
      category: selectedCategory ?? undefined,
      q: searchQuery ?? undefined,
      page: 1,
      pageSize: DISCOVERY_PAGE_SIZE,
      sort,
      minRating: minRating ?? undefined,
    });
    if (result.totalCount > 0) {
      initialPage = result;
    }
  } catch {
    loadError = t(locale, "discover.loadError");
  }

  const categoryRecord = categories.find((item) => item.slug === selectedCategory);
  const categoryName = categoryRecord
    ? getCategoryLabel(locale, categoryRecord.slug, categoryRecord.name)
    : null;
  const intentSummary = searchQuery ? parseSearchIntent(searchQuery).summary : null;

  const description = searchQuery
    ? t(locale, "discover.searchNearMarket", {
        summary: intentSummary ?? `“${searchQuery}”`,
        market: market.name,
      })
    : categoryName
      ? t(locale, "discover.nearMarketCategory", { market: market.name, category: categoryName })
      : t(locale, "discover.nearMarket", { market: market.name });

  return (
    <div className="flex flex-1 flex-col">
      <PublicHeader
        searchMode="hero-handoff"
        marketId={market.id}
        marketName={market.name}
        currency={market.currency}
        countryCode={market.countryCode}
        showBookingsNav={canAccessMyBookings()}
      />

      <main id="main-content" className={`${publicContainerClass} py-12 lg:py-14`}>
        <PageHeader title={t(locale, "discover.title")} description={description} />

        <div id={HERO_SEARCH_ANCHOR_ID} className="mt-8 scroll-mt-28">
          <HeroDiscoverySearch />
        </div>

        {categories.length > 0 ? (
          <CategoryFilterLinks
            categories={categories}
            selectedCategory={selectedCategory}
            searchQuery={searchQuery}
            className="mt-8"
          />
        ) : null}

        <DiscoverFilterBar
          category={selectedCategory}
          searchQuery={searchQuery}
          sort={sort}
          minRating={minRating}
          className="mt-4"
        />

        {loadError ? (
          <Callout tone="error" className="mt-8">
            {loadError}
          </Callout>
        ) : !initialPage ? (
          <EmptyState
            className="mt-8"
            title={
              searchQuery
                ? t(locale, "discover.noMatchesTitle")
                : t(locale, "discover.noBusinessesTitle")
            }
            description={
              searchQuery
                ? t(locale, "discover.noMatchesDescription")
                : selectedCategory
                  ? t(locale, "discover.noCategoryDescription")
                  : t(locale, "discover.noBusinessesDescription")
            }
            actionLabel={
              searchQuery ? t(locale, "discover.browseAll") : t(locale, "discover.listBusiness")
            }
            actionHref={searchQuery ? "/discover" : "/business/register"}
          />
        ) : (
          <DiscoverBusinessGrid
            lat={searchLocation.lat}
            lng={searchLocation.lng}
            market={market.id}
            category={selectedCategory}
            q={searchQuery}
            sort={sort}
            minRating={minRating}
            initialPage={initialPage}
          />
        )}
      </main>
    </div>
  );
}
