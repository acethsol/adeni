import {
  getCountryPricing,
  getSkuMonthlyAmount,
  type SubscriptionSku,
} from "./country-pricing";

/** Per-tenant SaaS overrides stored on the tenant (admin-set). */
export type TenantSaasPriceOverride = {
  customSaasMonthlyAmount?: number | null;
  customSaasCurrency?: string | null;
  customSaasSku?: SubscriptionSku | null;
};

export type TenantSaasDiscount = {
  discountPercent?: number | null;
  discountFixedAmount?: number | null;
  discountCurrency?: string | null;
};

/**
 * Catalog list price or admin override for a tenant SKU.
 * Checkout (Sprint 33) should call this before applying discount fields.
 */
export function resolveTenantSaasListPrice(
  countryCode: string,
  sku: SubscriptionSku,
  override?: TenantSaasPriceOverride | null,
): { amount: number; currency: string } | null {
  const customAmount = override?.customSaasMonthlyAmount;
  const customSku = override?.customSaasSku ?? null;
  if (
    typeof customAmount === "number" &&
    customAmount > 0 &&
    override?.customSaasCurrency &&
    (customSku === null || customSku === sku)
  ) {
    return {
      amount: customAmount,
      currency: override.customSaasCurrency.toUpperCase(),
    };
  }

  const catalogAmount = getSkuMonthlyAmount(countryCode, sku);
  const pricing = getCountryPricing(countryCode);
  if (catalogAmount == null || !pricing) {
    return null;
  }

  return { amount: catalogAmount, currency: pricing.currency };
}

/** Apply tenant discount fields to a list price (fixed amount wins when set). */
export function applyTenantSaasDiscount(
  listPrice: number,
  discount?: TenantSaasDiscount | null,
): number {
  if (!discount) {
    return listPrice;
  }

  if (typeof discount.discountFixedAmount === "number" && discount.discountFixedAmount > 0) {
    return Math.max(0, listPrice - discount.discountFixedAmount);
  }

  if (typeof discount.discountPercent === "number" && discount.discountPercent > 0) {
    return Math.max(0, listPrice * (1 - discount.discountPercent / 100));
  }

  return listPrice;
}
