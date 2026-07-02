import type { Metadata } from "next";
import Link from "next/link";
import type { Category } from "@adeni/shared";
import { PublicHeader } from "@/components/public-header";
import { createApiClient, getApiBaseUrl } from "@/lib/adeni";
import { getActiveMarketConfig } from "@/lib/market";

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
    <div className="min-h-screen bg-[#f6f8f6] text-[#1b4332]">
      <PublicHeader />

      <main className="mx-auto max-w-5xl px-6 py-16">
        <section className="max-w-2xl">
          <p className="mb-3 text-sm font-semibold uppercase tracking-widest text-[#40916c]">
            {market.name}
          </p>
          <h1 className="text-4xl font-bold leading-tight tracking-tight sm:text-5xl">
            {market.tagline}
          </h1>
          <p className="mt-4 text-lg text-[#1b4332]/80">{market.description}</p>
          <p className="mt-3 text-sm text-[#1b4332]/70">{market.launchNote}</p>
          <div className="mt-8 flex flex-wrap gap-3">
            <Link
              href="/discover"
              className="rounded-full bg-[#1b4332] px-6 py-3 text-sm font-semibold text-white"
            >
              Browse services
            </Link>
            <Link
              href="/business"
              className="rounded-full border border-[#1b4332]/20 px-6 py-3 text-sm font-semibold"
            >
              List your business
            </Link>
          </div>
        </section>

        <section className="mt-16">
          <h2 className="text-lg font-semibold">Categories</h2>
          {categories.length === 0 ? (
            <p className="mt-4 text-sm text-[#1b4332]/70">
              Start the API at {getApiBaseUrl()} to load categories.
            </p>
          ) : (
            <div className="mt-4 space-y-8">
              {[...groupedCategories.entries()].map(([parentSlug, items]) => (
                <div key={parentSlug}>
                  <h3 className="text-sm font-semibold uppercase tracking-wide text-[#40916c]">
                    {formatGroupLabel(parentSlug)}
                  </h3>
                  <ul className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
                    {items.map((category) => (
                      <li key={category.id}>
                        <Link
                          href={`/discover?category=${category.slug}`}
                          className="block rounded-xl border border-[#1b4332]/10 bg-white p-4 shadow-sm transition hover:border-[#40916c]/40"
                        >
                          <p className="font-medium">{category.name}</p>
                          <p className="text-sm text-[#1b4332]/60">{category.slug}</p>
                        </Link>
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
