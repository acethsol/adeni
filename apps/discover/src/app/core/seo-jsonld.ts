import {
  localBusinessSchemaType,
  type PublicBusinessProfile,
  type ServiceOffering,
} from "@adeni/shared";

export function buildLocalBusinessJsonLd(
  profile: PublicBusinessProfile,
  publicUrl: string,
  coverImageUrl: string,
  services: ServiceOffering[] = [],
): Record<string, unknown> {
  const images = uniqueImages([
    coverImageUrl,
    ...(profile.imageUrls ?? []),
    profile.coverImageUrl ?? "",
  ]);

  const jsonLd: Record<string, unknown> = {
    "@context": "https://schema.org",
    "@type": localBusinessSchemaType(profile.categorySlug),
    name: profile.name,
    description: profile.description || undefined,
    url: publicUrl,
    image: images.length <= 1 ? images[0] : images,
    address: {
      "@type": "PostalAddress",
      streetAddress: profile.addressLine || undefined,
      addressLocality: profile.area || undefined,
      addressCountry: countryCodeFromMarket(profile.marketId),
    },
    areaServed: {
      "@type": "City",
      name: cityNameFromMarket(profile.marketId, profile.area),
    },
  };

  const phone = usableTelephone(profile.phoneMasked);
  if (phone) {
    jsonLd["telephone"] = phone;
  }

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

  const hours = (profile.openingHours ?? [])
    .filter((h) => h.dayOfWeek && h.openTime && h.closeTime)
    .map((h) => ({
      "@type": "OpeningHoursSpecification",
      dayOfWeek: `https://schema.org/${h.dayOfWeek}`,
      opens: h.openTime,
      closes: h.closeTime,
    }));
  if (hours.length > 0) {
    jsonLd["openingHoursSpecification"] = hours;
  }

  const offers = services
    .filter((s) => s.isActive !== false && s.pricingType !== "quote_request")
    .slice(0, 24)
    .map((s) => {
      const offer: Record<string, unknown> = {
        "@type": "Offer",
        name: s.name,
        url: `${publicUrl}#services`,
        priceCurrency: s.currency,
      };
      if (typeof s.priceAmount === "number" && Number.isFinite(s.priceAmount)) {
        offer["price"] = s.priceAmount;
      }
      return offer;
    });
  if (offers.length > 0) {
    jsonLd["hasOfferCatalog"] = {
      "@type": "OfferCatalog",
      name: "Services",
      itemListElement: offers,
    };
  }

  return jsonLd;
}

function usableTelephone(masked: string | null | undefined): string | undefined {
  if (!masked?.trim()) {
    return undefined;
  }
  const value = masked.trim();
  if (/redacted|masked|\*{3,}|•{3,}/i.test(value)) {
    return undefined;
  }
  const digits = value.replace(/\D/g, "");
  if (digits.length < 7) {
    return undefined;
  }
  return value;
}

function countryCodeFromMarket(marketId: string): string {
  const id = marketId.trim().toLowerCase();
  if (id === "ottawa") {
    return "CA";
  }
  if (id === "lagos") {
    return "NG";
  }
  return marketId.toUpperCase().slice(0, 2);
}

function cityNameFromMarket(marketId: string, area: string): string {
  const id = marketId.trim().toLowerCase();
  if (id === "ottawa") {
    return area?.trim() || "Ottawa";
  }
  if (id === "lagos") {
    return area?.trim() || "Lagos";
  }
  return area?.trim() || marketId;
}

function uniqueImages(urls: string[]): string[] {
  const seen = new Set<string>();
  const out: string[] = [];
  for (const raw of urls) {
    const url = raw?.trim();
    if (!url || seen.has(url)) {
      continue;
    }
    seen.add(url);
    out.push(url);
  }
  return out;
}
