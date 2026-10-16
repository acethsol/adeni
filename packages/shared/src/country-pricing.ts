import catalog from "./data/country-pricing.json";
import { formatPrice } from "./format";
import type { SubscriptionTier } from "./schemas";
import { z } from "zod";

export const SUBSCRIPTION_SKUS = ["starter", "professional", "business"] as const;
export type SubscriptionSku = (typeof SUBSCRIPTION_SKUS)[number];

export const SUBSCRIPTION_SKU_LABELS: Record<SubscriptionSku, string> = {
  starter: "Starter",
  professional: "Professional",
  business: "Business",
};

export function formatSubscriptionSku(sku: SubscriptionSku | null | undefined): string {
  if (!sku) {
    return "—";
  }
  return SUBSCRIPTION_SKU_LABELS[sku] ?? sku;
}

/** Prefer stored SKU; fall back to legacy display tier mapping. */
export function resolveSubscriptionSku(
  sku: SubscriptionSku | null | undefined,
  legacyTier?: SubscriptionTier | null,
): SubscriptionSku {
  if (sku) {
    return sku;
  }
  if (legacyTier) {
    return subscriptionTierToSku(legacyTier);
  }
  return "starter";
}

export const GMV_FEE_BEARERS = ["business"] as const;
export type GmvFeeBearer = (typeof GMV_FEE_BEARERS)[number];

export const GMV_COLLECTION_MODES = ["paystack_split", "stripe_connect", "none"] as const;
export type GmvCollectionMode = (typeof GMV_COLLECTION_MODES)[number];

const skuPriceSchema = z.object({
  monthlyAmount: z.number().positive().nullable().optional(),
});

const gmvCollectionSchema = z.object({
  feeBearer: z.enum(GMV_FEE_BEARERS).default("business"),
  mode: z.enum(["paystack_split", "stripe_connect"]),
  platformFeePercent: z.number().min(0).max(100),
  requireMerchantOnboarding: z.boolean(),
  blockPaymentsUntilOnboarding: z.boolean(),
  splitCode: z.string().nullable().optional(),
});

const countryPricingSchema = z.object({
  currency: z.string().length(3),
  paymentProvider: z.enum(["paystack", "stripe"]),
  skus: z.object({
    starter: skuPriceSchema,
    professional: skuPriceSchema,
    business: skuPriceSchema,
  }),
  gmvCollection: gmvCollectionSchema,
});

const countryPricingCatalogSchema = z.object({
  gmvFeeBearer: z.enum(GMV_FEE_BEARERS).default("business"),
  countries: z.record(z.string().length(2), countryPricingSchema),
});

export type CountryPricing = z.infer<typeof countryPricingSchema>;
export type GmvCollectionConfig = z.infer<typeof gmvCollectionSchema>;
export type CountryPricingCatalog = z.infer<typeof countryPricingCatalogSchema>;

const parsedCatalog = countryPricingCatalogSchema.parse(catalog);

const SKU_DISPLAY: Record<
  SubscriptionSku,
  { name: string; description: string }
> = {
  starter: {
    name: "Starter",
    description: "Profile, calendar, bookings, basic dashboard, and WhatsApp bridge.",
  },
  professional: {
    name: "Professional",
    description: "Starter plus messaging, reminders, and analytics dashboard.",
  },
  business: {
    name: "Business",
    description: "Professional plus multi-location, staff tools, and priority support.",
  },
};

/** Maps legacy API tier values to commercial SKUs. */
export function subscriptionTierToSku(tier: SubscriptionTier): SubscriptionSku {
  switch (tier) {
    case "pro":
      return "professional";
    case "business":
      return "business";
    default:
      return "starter";
  }
}

export function skuToSubscriptionTier(sku: SubscriptionSku): SubscriptionTier {
  switch (sku) {
    case "professional":
      return "pro";
    case "business":
      return "business";
    default:
      return "free";
  }
}

export function normalizeCountryCode(countryCode: string): string {
  return countryCode.trim().toUpperCase();
}

export function getCountryPricingCatalog(): CountryPricingCatalog {
  return parsedCatalog;
}

export function getCountryPricing(countryCode: string): CountryPricing | null {
  const key = normalizeCountryCode(countryCode);
  return parsedCatalog.countries[key] ?? null;
}

export function getGmvCollection(countryCode: string): GmvCollectionConfig | null {
  return getCountryPricing(countryCode)?.gmvCollection ?? null;
}

export function listPricedCountryCodes(): string[] {
  return Object.keys(parsedCatalog.countries).sort();
}

export function getSkuMonthlyAmount(countryCode: string, sku: SubscriptionSku): number | null {
  const country = getCountryPricing(countryCode);
  if (!country) return null;
  const row = country.skus[sku];
  const amount = row.monthlyAmount;
  return typeof amount === "number" && amount > 0 ? amount : null;
}

export function formatSkuMonthlyPrice(
  countryCode: string,
  sku: SubscriptionSku,
  locale?: string,
): string | null {
  const country = getCountryPricing(countryCode);
  const amount = getSkuMonthlyAmount(countryCode, sku);
  if (!country || amount == null) return null;
  return `${formatPrice(amount, country.currency, locale)}/mo`;
}

export function getPlanDisplayForCountry(
  countryCode: string,
  sku: SubscriptionSku,
): { name: string; priceLabel: string; description: string; currency: string } | null {
  const country = getCountryPricing(countryCode);
  const amount = getSkuMonthlyAmount(countryCode, sku);
  if (!country || amount == null) return null;
  const meta = SKU_DISPLAY[sku];
  return {
    name: meta.name,
    description: meta.description,
    currency: country.currency,
    priceLabel: formatSkuMonthlyPrice(countryCode, sku) ?? "",
  };
}

/** Effective GMV platform % (0 when collection not ready — callers pass collectionReady). */
export function getEffectivePlatformFeePercent(
  countryCode: string,
  collectionReady: boolean,
): number {
  const gmv = getGmvCollection(countryCode);
  if (!gmv || !collectionReady) return 0;
  return gmv.platformFeePercent;
}
