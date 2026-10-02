import type { PublicBusinessProfile } from "@adeni/shared";

export function buildLocalBusinessJsonLd(
  profile: PublicBusinessProfile,
  publicUrl: string,
  coverImageUrl: string,
): Record<string, unknown> {
  const jsonLd: Record<string, unknown> = {
    "@context": "https://schema.org",
    "@type": "LocalBusiness",
    name: profile.name,
    description: profile.description || undefined,
    url: publicUrl,
    image: coverImageUrl,
    address: {
      "@type": "PostalAddress",
      streetAddress: profile.addressLine,
      addressLocality: profile.area,
      addressCountry: profile.marketId.toUpperCase().slice(0, 2),
    },
    telephone: profile.phoneMasked,
  };

  if (profile.latitude != null && profile.longitude != null) {
    jsonLd["geo"] = {
      "@type": "GeoCoordinates",
      latitude: profile.latitude,
      longitude: profile.longitude,
    };
  }

  if (profile.ratingAvg != null && profile.reviewCount) {
    jsonLd["aggregateRating"] = {
      "@type": "AggregateRating",
      ratingValue: profile.ratingAvg,
      reviewCount: profile.reviewCount,
    };
  }

  return jsonLd;
}
