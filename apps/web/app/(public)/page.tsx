import type { Metadata } from "next";
import type { Category, DiscoveryBusinessItem } from "@adeni/shared";
import { CalendarCheck, MapPin, ShieldCheck } from "lucide-react";
import { t, getCategoryLabel, getCategoryGroupLabel, getMarketTagline, getMarketDescription } from "@adeni/shared";
import { PublicHeader } from "@/components/public-header";
import { HERO_SEARCH_ANCHOR_ID } from "@/lib/hero-search";
import { canAccessMyBookings } from "@/lib/customer-access";
import { HeroDiscoverySearch } from "@/components/hero-discovery-search";
import { CategoryTile } from "@/components/category-tile";
import { BusinessDiscoveryCard } from "@/components/business-discovery-card";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { CategoryChip, SectionHeader } from "@/components/ui/section-header";
import { createApiClient, getApiBaseUrl } from "@/lib/adeni";
import {
  publicCardGridClass,
  publicContainerClass,
  publicHeroBandClass,
} from "@/lib/layout-classes";
import { getActiveMarketConfig, getDiscoveryLocation } from "@/lib/market";
import { getLocale } from "@/lib/locale";
import { getCategoryVisual, FEATURED_PAGE_SIZE } from "@adeni/shared";

export const revalidate = 3600;

export async function generateMetadata(): Promise<Metadata> {
  const [market, locale] = await Promise.all([getActiveMarketConfig(), getLocale()]);
  const tagline = getMarketTagline(locale);
  const description = getMarketDescription(locale);

  return {
    title: t(locale, "home.metaTitle", { tagline, market: market.name }),
    description: t(locale, "home.metaDescription", {
      market: market.name,
      description,
    }),
  };
}

async function getCategories(): Promise<Category[]> {
  try {
    const client = createApiClient();
    return await client.getCategories();
  } catch {
    return [];
  }
}

async function getFeaturedBusinesses(
  marketId: string,
  lat: number,
  lng: number,
): Promise<DiscoveryBusinessItem[]> {
  try {
    const client = createApiClient();
    const result = await client.searchDiscovery({
      lat,
      lng,
      market: marketId,
      page: 1,
      pageSize: FEATURED_PAGE_SIZE,
      sort: "featured",
    });
    return result.items;
  } catch {
    return [];
  }
}

function groupCategories(categories: Category[]) {
  const groups = new Map<string, Category[]>();

  for (const category of categories) {
    const groupKey = category.parentSlug ?? "general";
    const existing = groups.get(groupKey) ?? [];
    existing.push(category);
    groups.set(groupKey, existing);
  }

  return groups;
}

function formatGroupLabel(parentSlug: string, locale: Awaited<ReturnType<typeof getLocale>>) {
  return getCategoryGroupLabel(locale, parentSlug);
}

const trustItemKeys = [
  { icon: ShieldCheck, key: "home.verifiedProviders" },
  { icon: CalendarCheck, key: "home.bookInMinutes" },
  { icon: MapPin, key: "home.trustedNearYou" },
] as const;

export default async function HomePage() {
  const [market, locale] = await Promise.all([getActiveMarketConfig(), getLocale()]);
  const searchLocation = await getDiscoveryLocation();
  const categories = await getCategories();
  const groupedCategories = groupCategories(categories);
  const featuredBusinesses = await getFeaturedBusinesses(
    market.id,
    searchLocation.lat,
    searchLocation.lng,
  );

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

      <section className={publicHeroBandClass}>
        <div className={`${publicContainerClass} py-12 lg:py-16`}>
          <div className="mx-auto max-w-3xl text-center">
            <h1 className="text-4xl font-bold tracking-tight text-foreground sm:text-5xl">
              {getMarketTagline(locale)}
            </h1>
            <p className="mx-auto mt-4 max-w-2xl text-base leading-relaxed text-muted sm:text-lg">
              {getMarketDescription(locale)}
            </p>

            <div id={HERO_SEARCH_ANCHOR_ID} className="mt-8 scroll-mt-28">
              <HeroDiscoverySearch className="mx-auto max-w-2xl" />
            </div>

            <ul className="mt-8 flex flex-wrap items-center justify-center gap-x-8 gap-y-3 text-sm text-muted">
              {trustItemKeys.map(({ icon: Icon, key }) => (
                <li key={key} className="inline-flex items-center gap-2">
                  <Icon className="h-4 w-4 text-accent" aria-hidden />
                  <span>{t(locale, key)}</span>
                </li>
              ))}
            </ul>

            <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
              <Button href="/discover">{t(locale, "home.browseServices")}</Button>
              <Button href="/business" variant="secondary">
                {t(locale, "home.listBusiness")}
              </Button>
            </div>

            {market.launchNote ? (
              <Callout tone="info" className="mx-auto mt-6 max-w-xl text-left">
                {market.launchNote}
              </Callout>
            ) : null}
          </div>
        </div>
      </section>

      <main className={`${publicContainerClass} py-12 lg:py-16`}>
        {featuredBusinesses.length > 0 ? (
          <section>
            <SectionHeader
              title={t(locale, "home.popularNearYou")}
              description={t(locale, "home.popularDescription")}
              action={
                <Button href="/discover" variant="ghost" size="sm">
                  {t(locale, "home.seeAll")}
                </Button>
              }
            />
            <ul className={`${publicCardGridClass} mt-6`}>
              {featuredBusinesses.map((business) => (
                <li key={business.tenantId}>
                  <BusinessDiscoveryCard business={business} />
                </li>
              ))}
            </ul>
          </section>
        ) : null}

        <section className={featuredBusinesses.length > 0 ? "mt-16 lg:mt-20" : ""}>
          <SectionHeader
            title={t(locale, "home.browseCategory")}
            description={t(locale, "home.browseCategoryDescription")}
          />

          {categories.length === 0 ? (
            <Callout tone="warning" className="mt-6">
              {t(locale, "home.apiOffline", { url: getApiBaseUrl() })}
            </Callout>
          ) : (
            <>
              <div className="mt-6 flex flex-wrap gap-2">
                {categories.map((category) => {
                  const label = getCategoryLabel(locale, category.slug, category.name);
                  const visual = getCategoryVisual(category.slug, label);
                  return (
                    <CategoryChip
                      key={category.id}
                      href={`/discover?category=${category.slug}`}
                      label={label}
                      icon={visual.icon}
                    />
                  );
                })}
              </div>

              <div className="mt-10 space-y-12">
                {[...groupedCategories.entries()].map(([parentSlug, items]) => (
                  <div key={parentSlug}>
                    <h3 className="text-sm font-semibold uppercase tracking-wide text-accent">
                      {getCategoryVisual(parentSlug, formatGroupLabel(parentSlug, locale)).icon}{" "}
                      {formatGroupLabel(parentSlug, locale)}
                    </h3>
                    <ul className={`${publicCardGridClass} mt-4`}>
                      {items.map((category) => (
                        <li key={category.id}>
                          <CategoryTile category={category} />
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </div>
            </>
          )}
        </section>
      </main>
    </div>
  );
}
