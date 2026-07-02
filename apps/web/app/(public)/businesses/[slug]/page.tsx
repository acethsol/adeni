import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { BookingPanel } from "@/components/booking-panel";
import { PublicHeader } from "@/components/public-header";
import { createApiClient } from "@/lib/adeni";
import { isAuth0Configured } from "@/lib/auth/config";
import { getOptionalSession } from "@/lib/auth/session";
import { getActiveMarketConfig } from "@/lib/market";

type Props = { params: Promise<{ slug: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const market = await getActiveMarketConfig();

  try {
    const client = createApiClient();
    const profile = await client.getBusinessProfile(slug);

    return {
      title: `${profile.name} — Adeni ${market.name}`,
      description: profile.description || `Verified ${profile.categorySlug} in ${profile.area}.`,
    };
  } catch {
    return {
      title: `Business — Adeni ${market.name}`,
    };
  }
}

export default async function BusinessProfilePage({ params }: Props) {
  const { slug } = await params;
  const returnPath = `/businesses/${slug}`;

  try {
    const client = createApiClient();
    const [profile, services] = await Promise.all([
      client.getBusinessProfile(slug),
      client.getBusinessServices(slug).catch(() => []),
    ]);

    const session = await getOptionalSession();
    const bookingEnabled =
      Boolean(session) || (!isAuth0Configured() && Boolean(process.env.DEV_CUSTOMER_AUTH0_SUB));

    return (
      <div className="min-h-screen bg-[#f6f8f6] text-[#1b4332]">
        <PublicHeader />

        <main className="mx-auto max-w-3xl px-6 py-16">
          <Link href="/discover" className="text-sm font-medium text-[#40916c]">
            ← Back to discover
          </Link>

          <div className="mt-6 rounded-2xl border border-[#1b4332]/10 bg-white p-8 shadow-sm">
            <p className="text-sm font-semibold uppercase tracking-widest text-[#40916c]">
              Verified business
            </p>
            <h1 className="mt-2 text-3xl font-bold">{profile.name}</h1>
            <p className="mt-2 text-[#1b4332]/80">
              {profile.locationName !== profile.name ? `${profile.locationName} · ` : ""}
              {profile.area} · {profile.categorySlug.replace("-", " ")}
            </p>

            {profile.description ? (
              <p className="mt-6 leading-relaxed text-[#1b4332]/85">
                {profile.description}
              </p>
            ) : null}

            <dl className="mt-8 grid gap-4 text-sm sm:grid-cols-2">
              <div>
                <dt className="font-medium text-[#1b4332]/60">Address</dt>
                <dd className="mt-1">{profile.addressLine}</dd>
              </div>
              <div>
                <dt className="font-medium text-[#1b4332]/60">Phone</dt>
                <dd className="mt-1">{profile.phoneMasked}</dd>
              </div>
            </dl>
          </div>

          <BookingPanel
            slug={slug}
            tenantId={profile.tenantId}
            services={services}
            bookingEnabled={bookingEnabled}
            loginHref={`/auth/login?returnTo=${encodeURIComponent(returnPath)}`}
          />
        </main>
      </div>
    );
  } catch {
    notFound();
  }
}
