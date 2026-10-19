import { describe, expect, it } from "vitest";
import { publicCountryPricingSchema } from "./schemas";

describe("publicCountryPricingSchema", () => {
  it("parses API-shaped NG payload", () => {
    const parsed = publicCountryPricingSchema.parse({
      countryCode: "NG",
      currencyCode: "NGN",
      pricingCatalogVersion: "2026-launch-01",
      selfServeEnabled: true,
      platformFeePercent: 0,
      skus: [
        { sku: "starter", monthlyAmountMajor: 35000 },
        { sku: "professional", monthlyAmountMajor: 55000 },
        { sku: "business", monthlyAmountMajor: 95000 },
      ],
    });
    expect(parsed.countryCode).toBe("NG");
  });
});
