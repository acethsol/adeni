import type { Metadata } from "next";
import { notFound } from "next/navigation";
import {
  getBusinessCoverImage,
  getCategoryLabel,
  getReviewCountLabel,
  resolveBusinessCoverImage,
  t,
} from "@adeni/shared";
import { BookingPanel } from "@/components/booking-panel";
import { BusinessReviewsSection } from "@/components/business-reviews-section";
import { StarRating } from "@/components/star-rating";
import { BackLink } from "@/components/ui/back-link";
import { RemoteImage } from "@/components/ui/remote-image";
import { PublicHeader } from "@/components/public-header";
import { createApiClient } from "@/lib/adeni";
import { isAuth0Configured } from "@/lib/auth/config";
import { getOptionalSession } from "@/lib/auth/session";
import { getTranslationPreference, translateMany } from "@/lib/content-translation";
import { getLocale } from "@/lib/locale";
import { getActiveMarketConfig } from "@/lib/market";

type Props = { params: Promise<{ slug: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const [market, locale, translateContent] = await Promise.all([
    getActiveMarketConfig(),
    getLocale(),
    getTranslationPreference(),
  ]);

  try {
    const client = createApiClient();
    const profile = await client.getBusinessProfile(slug);
    const [description] = await translateMany(
      [
        profile.description ||
          `Verified ${getCategoryLabel(locale, profile.categorySlug)} in ${profile.area}.`,
      ],
      locale,
      translateContent,
    );

    return {
      title: `${profile.name} — Adeni ${market.name}`,
      description,
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
    const [profile, services, reviews, locale, translateContent] = await Promise.all([
      client.getBusinessProfile(slug),
      client.getBusinessServices(slug).catch(() => []),
      client
        .getBusinessReviews(slug)
        .catch(() => ({ items: [], page: 1, pageSize: 10, totalCount: 0 })),
      getLocale(),
      getTranslationPreference(),
    ]);

    const fields: Array<string | null | undefined> = [];
    if (profile.description) {
      fields.push(profile.description);
    }
    for (const service of services) {
      fields.push(service.name);
      fields.push(service.description);
    }

    const translatedCopy = await translateMany(fields, locale, translateContent);

    let copyIndex = 0;
    const translatedDescription = profile.description
      ? translatedCopy[copyIndex++] ?? profile.description
      : null;

    const translatedServices = services.map((service) => {
      const name = translatedCopy[copyIndex++] ?? service.name;
      const description = service.description
        ? translatedCopy[copyIndex++] ?? service.description
        : service.description;
      return { ...service, name, description };
    });

    const session = await getOptionalSession();
    const bookingEnabled =
      Boolean(session) || (!isAuth0Configured() && Boolean(process.env.DEV_CUSTOMER_AUTH0_SUB));
    const coverImageUrl = resolveBusinessCoverImage(profile.categorySlug, profile.coverImageUrl);
    const coverFallbackUrl = getBusinessCoverImage(profile.categorySlug);
    const categoryLabel = getCategoryLabel(locale, profile.categorySlug);
    const reviewLabel = profile.reviewCount
      ? getReviewCountLabel(locale, profile.reviewCount)
      : null;
    const descriptionWasTranslated = Boolean(
      translateContent &&
        profile.description &&
        translatedDescription &&
        translatedDescription !== profile.description,
    );

    return (
      <div className="flex flex-1 flex-col">
        <PublicHeader searchMode="compact" />

        <main id="main-content" className="mx-auto max-w-3xl px-6 py-16">
          <BackLink
            href="/discover"
            label={t(locale, "business.backToDiscover")}
            hint={t(locale, "discover.title")}
            variant="trail"
          />

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

          <div className="mt-6 rounded-2xl border border-border bg-surface p-8 shadow-sm">
            <p className="text-sm font-semibold uppercase tracking-widest text-accent">
              {t(locale, "business.verifiedBusiness")}
            </p>
            <h1 className="mt-2 text-3xl font-bold">{profile.name}</h1>
            <p className="mt-2 text-muted">
              {profile.locationName !== profile.name ? `${profile.locationName} · ` : ""}
              {profile.area} · {categoryLabel}
            </p>
            {profile.reviewCount ? (
              <div className="mt-3 flex items-center gap-2 text-sm text-muted">
                <StarRating rating={profile.ratingAvg ?? 0} size="md" />
                <span>
                  {(profile.ratingAvg ?? 0).toFixed(1)} · {reviewLabel}
                </span>
              </div>
            ) : null}

            {translatedDescription ? (
              <p className="mt-6 leading-relaxed text-foreground">
                {translatedDescription}
                {descriptionWasTranslated ? (
                  <span className="ml-2 text-[10px] font-semibold uppercase tracking-wide text-muted">
                    {t(locale, "content.translated")}
                  </span>
                ) : null}
              </p>
            ) : null}

            <dl className="mt-8 grid gap-4 text-sm sm:grid-cols-2">
              <div>
                <dt className="font-medium text-muted">{t(locale, "business.address")}</dt>
                <dd className="mt-1">{profile.addressLine}</dd>
              </div>
              <div>
                <dt className="font-medium text-muted">{t(locale, "business.phone")}</dt>
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
            services={translatedServices}
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
