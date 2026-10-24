import { describe, expect, it } from "vitest";
import { z } from "zod";
import catalog from "./data/plan-entitlements.json";
import type { PlanEntitlementSeed } from "./plan-entitlements";
import type { SubscriptionSku } from "./country-pricing";
import { subscriptionSkuSchema, tenantEntitlementsSchema } from "./schemas";

// Compile-time exact keys, not a cast: every JSON row must match the handwritten TS seed contract.
type EqualKeys<A, B> = [keyof A] extends [keyof B] ? [keyof B] extends [keyof A] ? true : false : false;
const skuKeysMatch: EqualKeys<typeof catalog, Record<SubscriptionSku, unknown>> = true;
const rowKeysMatch: { [Sku in SubscriptionSku]: EqualKeys<typeof catalog[Sku], PlanEntitlementSeed> } = {
  starter: true, professional: true, business: true,
};
const typedRows: Record<SubscriptionSku, PlanEntitlementSeed> = catalog;

// These five fields are explicitly derived by JsonPlanEntitlementsMatrix, not catalogue inputs.
const seedSchema = tenantEntitlementsSchema.omit({
  invoicesReceipts: true, onlinePayments: true, deposits: true, advancedReminders: true, multiLocation: true,
}).required().strict();
const catalogSchema = z.object({ starter: seedSchema, professional: seedSchema, business: seedSchema }).strict();

describe("JSON / TypeScript entitlement contract parity", () => {
  it("has exactly the supported SKUs and the same seed keys as the API schema", () => {
    expect(skuKeysMatch).toBe(true);
    expect(Object.values(rowKeysMatch)).toEqual([true, true, true]);
    expect(Object.keys(catalog).sort()).toEqual([...subscriptionSkuSchema.options].sort());
    expect(catalogSchema.parse(typedRows)).toEqual(catalog);
  });

  for (const sku of subscriptionSkuSchema.options) {
    it(`${sku} has every required field with the correct runtime type`, () => {
      expect(seedSchema.parse(catalog[sku])).toEqual(catalog[sku]);
      for (const field of Object.keys(seedSchema.shape)) {
        const incomplete: Record<string, unknown> = { ...catalog[sku] };
        delete incomplete[field];
        expect(seedSchema.safeParse(incomplete).success, `${sku}.${field} must be required`).toBe(false);
        expect(seedSchema.safeParse({ ...catalog[sku], [field]: "wrong-type" }).success, `${sku}.${field} must be typed`).toBe(false);
      }
    });
  }

  it("keeps API compatibility and derived fields boolean without making them catalog authority", () => {
    for (const sku of subscriptionSkuSchema.options) {
      const seed = catalog[sku];
      const api = { ...seed, invoicesReceipts: true, onlinePayments: true, deposits: true,
        advancedReminders: seed.configurableReminders || seed.followUpAutomation,
        multiLocation: seed.maxLocations == null || seed.maxLocations > 1 };
      expect(tenantEntitlementsSchema.strict().parse(api)).toEqual(api);
      for (const key of ["invoicesReceipts", "onlinePayments", "deposits", "advancedReminders", "multiLocation"]) {
        expect(tenantEntitlementsSchema.safeParse({ ...api, [key]: "wrong-type" }).success).toBe(false);
      }
    }
  });

  it("rejects unsupported SKUs, missing rows and renamed fields", () => {
    expect(subscriptionSkuSchema.safeParse("enterprise").success).toBe(false);
    expect(catalogSchema.safeParse({ ...catalog, enterprise: catalog.business }).success).toBe(false);
    const { starter: omitted, ...missing } = catalog;
    expect(catalogSchema.safeParse(missing).success).toBe(false);
    const { messaging, ...renamed } = catalog.starter;
    expect(seedSchema.safeParse({ ...renamed, inbox: messaging }).success).toBe(false);
  });

  it("accepts nullable positive integer limits but rejects invalid limits and nonboolean flags", () => {
    expect(seedSchema.safeParse({ ...catalog.starter, maxLocations: null, maxPractitioners: null }).success).toBe(true);
    for (const invalid of [0, -1, 1.5, "3"]) {
      expect(seedSchema.safeParse({ ...catalog.starter, maxLocations: invalid }).success).toBe(false);
    }
    for (const invalid of [null, 1, "true"]) {
      expect(seedSchema.safeParse({ ...catalog.starter, messaging: invalid }).success).toBe(false);
    }
  });
});
