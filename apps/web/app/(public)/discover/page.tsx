import type { Metadata } from "next";
import type { DiscoveryBusinessItem } from "@adeni/shared";
import { BusinessDiscoveryCard } from "@/components/business-discovery-card";
import { Callout } from "@/components/ui/callout";
import { EmptyState } from "@/components/ui/empty-state";
import { PageHeader } from "@/components/ui/page-header";
import { PublicHeader } from "@/components/public-header";
import { HERO_SEARCH_ANCHOR_ID } from "@/lib/hero-search";
import { canAccessMyBookings } from "@/lib/customer-access";
import { HeroDiscoverySearch } from "@/components/hero-discovery-search";
import { CategoryFilterLinks } from "@/components/discover-category-filters";
import type { Category } from "@adeni/shared";
import { parseSearchIntent } from "@adeni/shared";
import { createApiClient } from "@/lib/adeni";
import { publicCardGridClass, publicContainerClass } from "@/lib/layout-classes";
import { getActiveMarketConfig, getDiscoveryLocation } from "@/lib/market";

export const revalidate = 120;

export async function generateMetadata(): Promise<Metadata> {
  const market = await getActiveMarketConfig();

  return {
    title: `Discover services in ${market.name} — Adeni`,
    description: `Find verified local service providers near you in ${market.name}.`,
  };
}

type Props = {
  searchParams: Promise<{ category?: string; q?: string }>;
};

async function getCategories(): Promise<Category[]> {
  try {
    const client = createApiClient();
    return await client.getCategories();
  } catch {
    return [];
  }
}

export default async function DiscoverPage({ searchParams }: Props) {
  const market = await getActiveMarketConfig();
  const searchLocation = await getDiscoveryLocation();
  const { category, q } = await searchParams;
  const categories = await getCategories();
  const selectedCategory = category?.trim().toLowerCase() || null;
  const searchQuery = q?.trim() || null;

  let businesses: DiscoveryBusinessItem[] = [];
  let loadError: string | null = null;

  try {
    const client = createApiClient();
    const result = await client.searchDiscovery({
      lat: searchLocation.lat,
      lng: searchLocation.lng,
      market: market.id,
      category: selectedCategory,
      q: searchQuery,
      pageSize: 50,
    });
    businesses = result.items;
  } catch {
    loadError = "Could not load nearby businesses. Is the API running?";
  }

  const categoryName =
    categories.find((item) => item.slug === selectedCategory)?.name ?? null;
  const intentSummary = searchQuery ? parseSearchIntent(searchQuery).summary : null;

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

      <main className={`${publicContainerClass} py-12 lg:py-14`}>
        <PageHeader
          title="Discover services"
          description={
            searchQuery
              ? `${intentSummary ?? `Results for “${searchQuery}”`} near ${market.name}.`
              : `Showing results near ${market.name}${categoryName ? ` · ${categoryName}` : ""}.`
          }
        />

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

        {loadError ? (
          <Callout tone="error" className="mt-8">
            {loadError}
          </Callout>
        ) : businesses.length === 0 ? (
          <EmptyState
            className="mt-8"
            title={searchQuery ? "No matches found" : "No verified businesses yet"}
            description={
              searchQuery
                ? "Try Ask Adeni with different words or browse all categories."
                : selectedCategory
                  ? "Try another category or list your business on Adeni."
                  : "Onboard supply via the business portal to appear here."
            }
            actionLabel={searchQuery ? "Browse all" : "List your business"}
            actionHref={searchQuery ? "/discover" : "/business/register"}
          />
        ) : (
          <ul className={`${publicCardGridClass} mt-8`}>
            {businesses.map((business) => (
              <li key={business.tenantId}>
                <BusinessDiscoveryCard business={business} />
              </li>
            ))}
          </ul>
        )}
      </main>
    </div>
  );
}
