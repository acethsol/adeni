import { describe, expect, it } from "vitest";
import {
  adminTenantCommercialTermsSchema,
  setTenantCommercialTermsRequestSchema,
} from "./schemas";

describe("admin commercial terms schemas", () => {
  it("preserves resolved pricing fields on GET response", () => {
    const parsed = adminTenantCommercialTermsSchema.parse({
      tenantId: "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      subscriptionSku: "professional",
      billingStatus: "active",
      billingCountryCode: "NG",
      catalogListPrice: 55000,
      effectiveMonthlyAmount: 49500,
      pricingCatalogVersion: "2026-launch-01",
      currency: "NGN",
    });

    expect(parsed.billingCountryCode).toBe("NG");
    expect(parsed.catalogListPrice).toBe(55000);
    expect(parsed.effectiveMonthlyAmount).toBe(49500);
    expect(parsed.pricingCatalogVersion).toBe("2026-launch-01");
    expect(parsed.currency).toBe("NGN");
  });

  it("accepts changeReason on PATCH request", () => {
    const parsed = setTenantCommercialTermsRequestSchema.parse({
      subscriptionSku: "professional",
      changeReason: "Negotiated enterprise rate",
    });

    expect(parsed.changeReason).toBe("Negotiated enterprise rate");
  });
});
