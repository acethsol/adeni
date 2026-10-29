import { buildLocalBusinessJsonLd } from "./seo-jsonld";
import type { PublicBusinessProfile } from "@adeni/shared";

function profile(overrides: Partial<PublicBusinessProfile> = {}): PublicBusinessProfile {
  return {
    locationId: "loc-1",
    tenantId: "00000000-0000-0000-0000-000000000001",
    slug: "lekki-cuts",
    name: "Lekki Cuts",
    locationName: "Lekki Cuts",
    description: "Barber studio",
    categorySlug: "hair-grooming",
    marketId: "lagos",
    area: "Lekki",
    addressLine: "12 Admiralty Way",
    phoneMasked: "[REDACTED]",
    coverImageUrl: "https://cdn.example.com/cover.jpg",
    imageUrls: ["https://cdn.example.com/a.jpg", "https://cdn.example.com/cover.jpg"],
    latitude: 6.45,
    longitude: 3.55,
    ratingAvg: 4.8,
    reviewCount: 12,
    ...overrides,
  };
}

describe("buildLocalBusinessJsonLd", () => {
  it("uses a specific schema type and omits masked telephone", () => {
    const jsonLd = buildLocalBusinessJsonLd(
      profile(),
      "https://adeni.com/businesses/lekki-cuts",
      "https://cdn.example.com/cover.jpg",
    );

    expect(jsonLd["@type"]).toBe("HairSalon");
    expect(jsonLd["telephone"]).toBeUndefined();
    expect(jsonLd["areaServed"]).toEqual({ "@type": "City", name: "Lekki" });
    expect(jsonLd["image"]).toEqual([
      "https://cdn.example.com/cover.jpg",
      "https://cdn.example.com/a.jpg",
    ]);
  });

  it("keeps dialable telephone values", () => {
    const jsonLd = buildLocalBusinessJsonLd(
      profile({ phoneMasked: "+234 801 234 5678" }),
      "https://adeni.com/businesses/lekki-cuts",
      "https://cdn.example.com/cover.jpg",
    );

    expect(jsonLd["telephone"]).toBe("+234 801 234 5678");
  });

  it("emits opening hours and offers when provided", () => {
    const jsonLd = buildLocalBusinessJsonLd(
      profile({
        openingHours: [{ dayOfWeek: "Monday", openTime: "09:00", closeTime: "18:00" }],
      }),
      "https://adeni.com/businesses/lekki-cuts",
      "https://cdn.example.com/cover.jpg",
      [
        {
          id: "svc-1",
          isAddOn: false,
          name: "Fade",
          priceAmount: 5000,
          currency: "NGN",
          pricingType: "fixed",
          durationMinutes: 30,
          isActive: true,
        },
      ],
    );

    expect(jsonLd["openingHoursSpecification"]).toEqual([
      {
        "@type": "OpeningHoursSpecification",
        dayOfWeek: "https://schema.org/Monday",
        opens: "09:00",
        closes: "18:00",
      },
    ]);
    const catalog = jsonLd["hasOfferCatalog"] as { itemListElement: { name: string }[] };
    expect(catalog.itemListElement[0]?.name).toBe("Fade");
  });
});
