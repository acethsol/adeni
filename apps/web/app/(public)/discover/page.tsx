import type { Metadata } from "next";
import Link from "next/link";
import type { Category } from "@adeni/shared";
import { PublicHeader } from "@/components/public-header";
import { Card } from "@/components/ui/card";
import { Callout } from "@/components/ui/callout";
import { EmptyState } from "@/components/ui/empty-state";
import { PageHeader } from "@/components/ui/page-header";
import { createApiClient } from "@/lib/adeni";
import { getActiveMarketConfig, getDiscoveryLocation } from "@/lib/market";
import { cn } from "@/lib/cn";

export const revalidate = 120;

export async function generateMetadata(): Promise<Metadata> {
  const market = await getActiveMarketConfig();

  return {
    title: `Discover services in ${market.name} — Adeni`,
    description: `Find verified local service providers near you in ${market.name}.`,
  };
}

type Props = {
  searchParams: Promise<{ category?: string }>;
};

async function getCategories(): Promise<Category[]> {
  try {
    const client = createApiClient();
    return await client.getCategories();
  } catch {
    return [];
  }
}

function filterChipClass(active: boolean) {
  return cn(
    "rounded-full px-4 py-2 text-sm font-semibold transition-colors",
    active
      ? "bg-primary text-primary-foreground shadow-sm"
      : "border border-border-strong bg-surface text-foreground hover:border-accent/40",
  );
}

export default async function DiscoverPage({ searchParams }: Props) {
  const market = await getActiveMarketConfig();
  const searchLocation = await getDiscoveryLocation();
  const { category } = await searchParams;
  const categories = await getCategories();
  const selectedCategory = category?.trim().toLowerCase() || null;

  let businesses: Awaited<ReturnType<ReturnType<typeof createApiClient>["searchDiscovery"]>>["items"] =
    [];
  let loadError: string | null = null;

  try {
    const client = createApiClient();
    const result = await client.searchDiscovery({
      lat: searchLocation.lat,
      lng: searchLocation.lng,
      market: market.id,
      category: selectedCategory,
    });
    businesses = result.items;
  } catch {
    loadError = "Could not load nearby businesses. Is the API running?";
  }

  const categoryName =
    categories.find((item) => item.slug === selectedCategory)?.name ?? null;

  return (
    <div className="min-h-screen bg-background text-foreground">
      <PublicHeader />

      <main className="mx-auto max-w-5xl px-6 py-16">
        <PageHeader
          eyebrow={market.name}
          title="Discover services"
          description={`Showing results near ${market.name}${categoryName ? ` · ${categoryName}` : ""}.`}
        />

        {categories.length > 0 && (
          <div className="mt-6 flex flex-wrap gap-2">
            <Link href="/discover" className={filterChipClass(!selectedCategory)}>
              All
            </Link>
            {categories.map((item) => (
              <Link
                key={item.id}
                href={`/discover?category=${item.slug}`}
                className={filterChipClass(selectedCategory === item.slug)}
              >
                {item.name}
              </Link>
            ))}
          </div>
        )}

        {loadError ? (
          <Callout tone="error" className="mt-8">
            {loadError}
          </Callout>
        ) : businesses.length === 0 ? (
          <EmptyState
            className="mt-8"
            title="No verified businesses yet"
            description={
              selectedCategory
                ? "Try another category or list your business on Adeni."
                : "Onboard supply via the business portal to appear here."
            }
            actionLabel="List your business"
            actionHref="/business/register"
          />
        ) : (
          <ul className="mt-8 grid gap-4 sm:grid-cols-2">
            {businesses.map((business) => (
              <li key={business.tenantId}>
                <Link href={`/businesses/${business.slug}`}>
                  <Card interactive padding="md" className="hover:border-accent/40">
                    <p className="text-lg font-semibold">{business.name}</p>
                    <p className="mt-1 text-sm text-muted">
                      {business.area} · {business.categorySlug.replace("-", " ")}
                    </p>
                    <p className="mt-2 text-xs font-semibold text-accent">
                      {business.distanceKm.toFixed(1)} km away
                    </p>
                  </Card>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </main>
    </div>
  );
}
