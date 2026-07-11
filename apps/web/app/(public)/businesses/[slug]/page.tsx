import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import {
  formatCategoryLabel,
  getBusinessCoverImage,
  resolveBusinessCoverImage,
} from "@adeni/shared";
import { BookingPanel } from "@/components/booking-panel";
import { BusinessReviewsSection } from "@/components/business-reviews-section";
import { StarRating } from "@/components/star-rating";
import { RemoteImage } from "@/components/ui/remote-image";
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
    const [profile, services, reviews] = await Promise.all([
      client.getBusinessProfile(slug),
      client.getBusinessServices(slug).catch(() => []),
      client.getBusinessReviews(slug).catch(() => ({ items: [], page: 1, pageSize: 10, totalCount: 0 })),
    ]);

    const session = await getOptionalSession();
    const bookingEnabled =
      Boolean(session) || (!isAuth0Configured() && Boolean(process.env.DEV_CUSTOMER_AUTH0_SUB));
    const coverImageUrl = resolveBusinessCoverImage(profile.categorySlug, profile.coverImageUrl);
    const coverFallbackUrl = getBusinessCoverImage(profile.categorySlug);

    return (
      <div className="flex flex-1 flex-col">
        <PublicHeader searchMode="compact" />

        <main className="mx-auto max-w-3xl px-6 py-16">
          <Link href="/discover" className="text-sm font-medium text-[#40916c]">
            ← Back to discover
          </Link>

          <div className="relative mt-6 aspect-[16/9] overflow-hidden rounded-xl bg-muted">
            <RemoteImage
              src={coverImageUrl}
              fallbackSrc={coverFallbackUrl}
              alt=""
              fill
              priority
              sizes="(max-width: 768px) 100vw, 768px"
              className="object-cover"
            />
          </div>

          <div className="mt-6 rounded-2xl border border-[#1b4332]/10 bg-white p-8 shadow-sm">
            <p className="text-sm font-semibold uppercase tracking-widest text-[#40916c]">
              Verified business
            </p>
            <h1 className="mt-2 text-3xl font-bold">{profile.name}</h1>
            <p className="mt-2 text-[#1b4332]/80">
              {profile.locationName !== profile.name ? `${profile.locationName} · ` : ""}
              {profile.area} · {formatCategoryLabel(profile.categorySlug)}
            </p>
            {profile.reviewCount ? (
              <div className="mt-3 flex items-center gap-2 text-sm text-[#1b4332]/80">
                <StarRating rating={profile.ratingAvg ?? 0} size="md" />
                <span>
                  {(profile.ratingAvg ?? 0).toFixed(1)} · {profile.reviewCount} review
                  {profile.reviewCount === 1 ? "" : "s"}
                </span>
              </div>
            ) : null}

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

          <BusinessReviewsSection
            reviews={reviews.items}
            ratingAvg={profile.ratingAvg}
            reviewCount={profile.reviewCount}
          />

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
