import { describe, expect, it } from "vitest";
import {
  formatSkuMonthlyPrice,
  getCountryPricing,
  getEffectivePlatformFeePercent,
  getGmvCollection,
  getSkuMonthlyAmount,
  listPricedCountryCodes,
  resolveSubscriptionSku,
  skuToSubscriptionTier,
  subscriptionTierToSku,
} from "./country-pricing";

describe("country-pricing", () => {
  it("lists NG, CA, US", () => {
    expect(listPricedCountryCodes()).toEqual(["CA", "NG", "US"]);
  });

  it("returns NG starter at 50000 NGN", () => {
    expect(getSkuMonthlyAmount("NG", "starter")).toBe(50000);
    expect(getSkuMonthlyAmount("ng", "professional")).toBe(70000);
    expect(getSkuMonthlyAmount("NG", "business")).toBe(100000);
  });

  it("returns US and CA ladder", () => {
    expect(getSkuMonthlyAmount("US", "starter")).toBe(150);
    expect(getSkuMonthlyAmount("US", "professional")).toBe(200);
    expect(getSkuMonthlyAmount("CA", "starter")).toBe(200);
  });

  it("formats monthly price with currency", () => {
    expect(formatSkuMonthlyPrice("US", "starter")).toMatch(/\$150\.00\/mo/);
    expect(formatSkuMonthlyPrice("NG", "starter")).toMatch(/50,000/);
  });

  it("maps legacy subscription tiers to SKUs", () => {
    expect(subscriptionTierToSku("free")).toBe("starter");
    expect(subscriptionTierToSku("pro")).toBe("professional");
    expect(subscriptionTierToSku("business")).toBe("business");
    expect(skuToSubscriptionTier("professional")).toBe("pro");
    expect(resolveSubscriptionSku("professional", "free")).toBe("professional");
    expect(resolveSubscriptionSku(undefined, "free")).toBe("starter");
  });

  it("NG gmv is paystack split at 0%", () => {
    const gmv = getGmvCollection("NG");
    expect(gmv?.mode).toBe("paystack_split");
    expect(gmv?.platformFeePercent).toBe(0);
    expect(gmv?.feeBearer).toBe("business");
  });

  it("NA gmv uses stripe connect draft rate", () => {
    expect(getCountryPricing("CA")?.gmvCollection.platformFeePercent).toBe(0.75);
  });

  it("effective platform fee is 0 until collection ready", () => {
    expect(getEffectivePlatformFeePercent("CA", false)).toBe(0);
    expect(getEffectivePlatformFeePercent("CA", true)).toBe(0.75);
    expect(getEffectivePlatformFeePercent("NG", true)).toBe(0);
  });
});
