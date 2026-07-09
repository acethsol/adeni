import type { Metadata } from "next";
import type { Category } from "@adeni/shared";
import { PublicHeader } from "@/components/public-header";
import { HeroDiscoverySearch } from "@/components/hero-discovery-search";
import { CategoryTile } from "@/components/category-tile";
import { Button } from "@/components/ui/button";
import { Callout } from "@/components/ui/callout";
import { PageHeader } from "@/components/ui/page-header";
import { createApiClient, getApiBaseUrl } from "@/lib/adeni";
import { getActiveMarketConfig } from "@/lib/market";
import { getCategoryVisual } from "@adeni/shared";

export const revalidate = 3600;

export async function generateMetadata(): Promise<Metadata> {
  const market = await getActiveMarketConfig();

  return {
    title: `Adeni — ${market.tagline} in ${market.name}`,
    description: `Discover verified local service providers in ${market.name}. ${market.description}`,
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

function formatGroupLabel(parentSlug: string) {
  if (parentSlug === "general") {
    return "General";
  }

  return parentSlug
    .split("-")
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(" ");
}

export default async function HomePage() {
  const market = await getActiveMarketConfig();
  const categories = await getCategories();
  const groupedCategories = groupCategories(categories);

  return (
    <div className="flex flex-1 flex-col">
      <PublicHeader searchMode="hero-handoff" />

      <main className="mx-auto max-w-5xl px-6 py-16">
        <section className="max-w-2xl">
          <PageHeader
            eyebrow={market.name}
            title={market.tagline}
            description={market.description}
            actions={
              <>
                <Button href="/discover">Browse services</Button>
                <Button href="/business" variant="secondary">
                  List your business
                </Button>
              </>
            }
          />
          <div className="mt-8">
            <HeroDiscoverySearch />
          </div>
          {market.launchNote ? (
            <Callout tone="info" className="mt-6">
              {market.launchNote}
            </Callout>
          ) : null}
        </section>

        <section className="mt-16">
          <h2 className="text-lg font-semibold">Browse by category</h2>
          {categories.length === 0 ? (
            <Callout tone="warning" className="mt-4">
              Start the API at {getApiBaseUrl()} to load categories.
            </Callout>
          ) : (
            <div className="mt-4 space-y-8">
              {[...groupedCategories.entries()].map(([parentSlug, items]) => (
                <div key={parentSlug}>
                  <h3 className="text-sm font-semibold uppercase tracking-wide text-accent">
                    {getCategoryVisual(parentSlug, formatGroupLabel(parentSlug)).icon}{" "}
                    {formatGroupLabel(parentSlug)}
                  </h3>
                  <ul className="mt-3 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                    {items.map((category) => (
                      <li key={category.id}>
                        <CategoryTile category={category} />
                      </li>
                    ))}
                  </ul>
                </div>
              ))}
            </div>
          )}
        </section>
      </main>
    </div>
  );
}
