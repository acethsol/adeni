import type { Metadata } from "next";
import Link from "next/link";
import type { Category } from "@adeni/shared";
import { PublicHeader } from "@/components/public-header";
import { createApiClient, getConfiguredMarket } from "@/lib/adeni";

export async function generateMetadata(): Promise<Metadata> {
  const market = getConfiguredMarket();

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

export default async function DiscoverPage({ searchParams }: Props) {
  const market = getConfiguredMarket();
  const { category } = await searchParams;
  const categories = await getCategories();
  const selectedCategory = category?.trim().toLowerCase() || null;

  let businesses: Awaited<ReturnType<ReturnType<typeof createApiClient>["searchDiscovery"]>>["items"] =
    [];
  let loadError: string | null = null;

  try {
    const client = createApiClient();
    const result = await client.searchDiscovery({
      lat: market.defaultLocation.lat,
      lng: market.defaultLocation.lng,
      category: selectedCategory,
    });
    businesses = result.items;
  } catch {
    loadError = "Could not load nearby businesses. Is the API running?";
  }

  const categoryName =
    categories.find((item) => item.slug === selectedCategory)?.name ?? null;

  return (
    <div className="min-h-screen bg-[#f6f8f6] text-[#1b4332]">
      <PublicHeader />

      <main className="mx-auto max-w-5xl px-6 py-16">
        <p className="text-sm font-semibold uppercase tracking-widest text-[#40916c]">
          {market.name}
        </p>
        <h1 className="mt-2 text-3xl font-bold">Discover services</h1>
        <p className="mt-3 text-[#1b4332]/80">
          Showing results near {market.name}
          {categoryName ? ` · ${categoryName}` : ""}.
        </p>

        {categories.length > 0 && (
          <div className="mt-6 flex flex-wrap gap-2">
            <Link
              href="/discover"
              className={`rounded-full px-4 py-2 text-sm font-medium ${
                selectedCategory
                  ? "border border-[#1b4332]/20 bg-white"
                  : "bg-[#1b4332] text-white"
              }`}
            >
              All
            </Link>
            {categories.map((item) => (
              <Link
                key={item.id}
                href={`/discover?category=${item.slug}`}
                className={`rounded-full px-4 py-2 text-sm font-medium ${
                  selectedCategory === item.slug
                    ? "bg-[#1b4332] text-white"
                    : "border border-[#1b4332]/20 bg-white"
                }`}
              >
                {item.name}
              </Link>
            ))}
          </div>
        )}

        {loadError ? (
          <p className="mt-8 text-sm text-[#1b4332]/70">{loadError}</p>
        ) : businesses.length === 0 ? (
          <p className="mt-8 text-sm text-[#1b4332]/70">
            No verified businesses found yet
            {selectedCategory ? ` for this category` : ""}. Onboard supply via the
            business portal.
          </p>
        ) : (
          <ul className="mt-8 grid gap-4 sm:grid-cols-2">
            {businesses.map((business) => (
              <li key={business.tenantId}>
                <Link
                  href={`/businesses/${business.slug}`}
                  className="block rounded-xl border border-[#1b4332]/10 bg-white p-5 shadow-sm transition hover:border-[#40916c]/40"
                >
                  <p className="text-lg font-semibold">{business.name}</p>
                  <p className="mt-1 text-sm text-[#1b4332]/70">
                    {business.area} · {business.categorySlug.replace("-", " ")}
                  </p>
                  <p className="mt-2 text-xs text-[#40916c]">
                    {business.distanceKm.toFixed(1)} km away
                  </p>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </main>
    </div>
  );
}
