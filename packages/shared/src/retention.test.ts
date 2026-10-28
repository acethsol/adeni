import { describe, expect, it } from "vitest";
import {
  appliedBenefitRequestSchema,
  createBookingRequestSchema,
  createPackageDefinitionRequestSchema,
  customerPackageSchema,
  customerProfileSchema,
  sellPackageRequestSchema,
} from "./index";
import { t } from "./i18n";

const id = "11111111-1111-4111-8111-111111111111";

describe("retention contracts", () => {
  it("requires at least one benefit and a 3-letter currency", () => {
    const base = { name: "Cut x3", listPrice: 100, currency: "NGN", benefits: [{ serviceOfferingId: id, quantityGranted: 3 }] };
    expect(createPackageDefinitionRequestSchema.safeParse(base).success).toBe(true);
    expect(createPackageDefinitionRequestSchema.safeParse({ ...base, benefits: [] }).success).toBe(false);
    expect(createPackageDefinitionRequestSchema.safeParse({ ...base, currency: "NAIRA" }).success).toBe(false);
  });

  it("only allows till or transfer when selling in person", () => {
    const base = { packageDefinitionId: id, businessLocationId: id };
    expect(sellPackageRequestSchema.safeParse({ ...base, paymentMethod: "till" }).success).toBe(true);
    expect(sellPackageRequestSchema.safeParse({ ...base, paymentMethod: "card" }).success).toBe(false);
  });

  it("accepts applied benefits on booking create and rejects non-positive quantities", () => {
    const booking = { tenantId: id, serviceOfferingId: id, startAt: "2026-11-02T10:00:00Z" };
    const applied = { customerPackageId: id, benefitBalanceId: id, quantity: 1 };
    expect(createBookingRequestSchema.safeParse({ ...booking, appliedBenefits: [applied] }).success).toBe(true);
    expect(appliedBenefitRequestSchema.safeParse({ ...applied, quantity: 0 }).success).toBe(false);
  });

  it("parses a customer package with balances", () => {
    const parsed = customerPackageSchema.parse({
      id, tenantId: id, tenantCustomerId: id, packageDefinitionId: id, name: "Cut x3", listPrice: 100, currency: "NGN",
      status: "active", soldChannel: "in_person_till", soldAtLocationId: id, redemptionScope: "all_locations",
      redemptionLocationIds: [], activatedAt: "2026-10-25T00:00:00Z", expiresAt: "2027-10-25T00:00:00Z", voidedAt: null,
      voidReason: null, note: null,
      balances: [{ id, serviceOfferingId: id, serviceName: "Haircut", granted: 3, used: 0, reserved: 1, forfeited: 0, remaining: 2, expiresAt: null }],
    });
    expect(parsed.balances[0]?.remaining).toBe(2);
  });

  it("exposes retention availability and summary on the customer profile", () => {
    const profile = customerProfileSchema.parse({
      tenantCustomerId: id, asOf: "2026-10-25", locationId: null,
      availability: { booking: "available", finance: "restricted", messaging: "unscoped", preferences: "notImplemented", retention: "available" },
      visitSummary: null, nextAppointment: null, recordedOnlinePaymentsByCurrency: null,
      packages: { activeCount: 1, totalRemainingCredits: 2, activePackageNames: ["Cut x3"] },
    });
    expect(profile.packages?.totalRemainingCredits).toBe(2);
  });

  it("has English and French messages for every retention error code", () => {
    const codes = [
      "package_not_found", "package_inactive", "package_not_active", "package_expired", "benefit_not_found",
      "benefit_insufficient", "benefit_service_mismatch", "benefit_conflict", "scope_location_denied",
      "sell_location_denied", "idempotency_key_required", "idempotency_conflict", "owner_required", "validation",
    ];
    for (const locale of ["en", "fr"] as const) {
      for (const code of codes) {
        const message = t(locale, `errors.retention.${code}`);
        expect(message, `${locale}:${code}`).not.toBe(`errors.retention.${code}`);
      }
      expect(t(locale, "errors.entitlements.packages")).not.toBe("errors.entitlements.packages");
    }
  });
});
