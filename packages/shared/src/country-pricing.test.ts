import { describe, expect, it } from "vitest";
import {
  formatSkuMonthlyPrice,
  getCountryPricing,
  getEffectivePlatformFeePercent,
  getGmvCollection,
  getSkuMonthlyAmount,
  listPricedCountryCodes,
  resolveSubscriptionSku,
} from "./country-pricing";

describe("country-pricing", () => {
  it("lists NG, CA, US", () => {
    expect(listPricedCountryCodes()).toEqual(["CA", "NG", "US"]);
  });

  it("returns NG launch ladder", () => {
    expect(getSkuMonthlyAmount("NG", "starter")).toBe(35000);
    expect(getSkuMonthlyAmount("ng", "professional")).toBe(55000);
    expect(getSkuMonthlyAmount("NG", "business")).toBe(95000);
  });

  it("returns CA launch ladder and US has no self-serve list price", () => {
    expect(getSkuMonthlyAmount("CA", "starter")).toBe(99);
    expect(getSkuMonthlyAmount("CA", "professional")).toBe(149);
    expect(getSkuMonthlyAmount("CA", "business")).toBe(249);
    expect(getSkuMonthlyAmount("US", "starter")).toBeNull();
    expect(getSkuMonthlyAmount("US", "professional")).toBeNull();
  });

  it("formats monthly price with currency", () => {
    expect(formatSkuMonthlyPrice("CA", "starter")).toMatch(/\$99\.00\/mo/);
    expect(formatSkuMonthlyPrice("NG", "starter")).toMatch(/35,000/);
  });

  it("resolves subscription SKU with starter default", () => {
    expect(resolveSubscriptionSku("professional")).toBe("professional");
    expect(resolveSubscriptionSku(undefined)).toBe("starter");
  });

  it("NG gmv is paystack split at 0%", () => {
    const gmv = getGmvCollection("NG");
    expect(gmv?.mode).toBe("paystack_split");
    expect(gmv?.platformFeePercent).toBe(0);
    expect(gmv?.feeBearer).toBe("business");
  });

  it("CA and NG gmv catalog rate is 0% at launch", () => {
    expect(getCountryPricing("CA")?.gmvCollection.platformFeePercent).toBe(0);
    expect(getCountryPricing("NG")?.gmvCollection.platformFeePercent).toBe(0);
  });

  it("effective platform fee is 0 until collection ready", () => {
    expect(getEffectivePlatformFeePercent("CA", false)).toBe(0);
    expect(getEffectivePlatformFeePercent("CA", true)).toBe(0);
    expect(getEffectivePlatformFeePercent("NG", true)).toBe(0);
  });
});
