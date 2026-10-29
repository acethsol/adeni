import { describe, expect, it } from "vitest";
import {
  adjustLoyaltyPointsRequestSchema,
  appliedBenefitRequestSchema,
  createBookingRequestSchema,
  createMembershipPlanRequestSchema,
  createPackageDefinitionRequestSchema,
  customerMembershipSchema,
  customerMembershipStatusSchema,
  customerPackageSchema,
  customerProfileSchema,
  enrolMembershipRequestSchema,
  membershipSubscribeResponseSchema,
  loyaltyAccountSchema,
  loyaltyAdjustmentSchema,
  loyaltyProgramSchema,
  loyaltyTransactionKindSchema,
  packagePurchaseResponseSchema,
  publicMembershipPlanCatalogSchema,
  publicPackageCatalogSchema,
  sellPackageRequestSchema,
  tenantMembershipMrrResponseSchema,
  upsertLoyaltyProgramRequestSchema,
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

  it("validates membership plan requests (positive price, typed policies, at least one benefit)", () => {
    const base = { name: "Fade Club", price: 25000, currency: "NGN", benefits: [{ serviceOfferingId: id, quantityPerCycle: 2 }] };
    expect(createMembershipPlanRequestSchema.safeParse(base).success).toBe(true);
    expect(createMembershipPlanRequestSchema.safeParse({ ...base, billingInterval: "annually", rolloverPolicy: "carry_over_one_cycle" }).success).toBe(true);
    expect(createMembershipPlanRequestSchema.safeParse({ ...base, price: 0 }).success).toBe(false);
    expect(createMembershipPlanRequestSchema.safeParse({ ...base, billingInterval: "weekly" }).success).toBe(false);
    expect(createMembershipPlanRequestSchema.safeParse({ ...base, pastDueGraceDays: 31 }).success).toBe(false);
    expect(createMembershipPlanRequestSchema.safeParse({ ...base, benefits: [] }).success).toBe(false);
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

  it("parses the public storefront catalog and purchase response", () => {
    const catalog = publicPackageCatalogSchema.parse({
      onlinePurchaseEnabled: false,
      items: [{
        id, name: "Cut x3", description: null, listPrice: 100, currency: "NGN", expiryMonths: 6,
        redemptionScope: "all_locations", benefits: [{ id, serviceOfferingId: id, serviceName: "Haircut", quantityGranted: 3 }],
      }],
    });
    expect(catalog.items[0]?.benefits[0]?.quantityGranted).toBe(3);
    expect(packagePurchaseResponseSchema.parse({
      customerPackageId: id, paymentIntentId: id, status: "pending_payment", checkoutUrl: null,
    }).status).toBe("pending_payment");
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
      "sell_location_denied", "idempotency_key_required", "idempotency_conflict", "owner_required",
      "online_purchase_unavailable", "online_subscription_unavailable", "validation", "membership_plan_not_found", "membership_not_found",
      "membership_plan_inactive", "membership_already_active", "membership_invalid_state", "membership_freeze_not_allowed",
      "membership_renewal_not_due", "membership_not_redeemable",
    ];
    for (const locale of ["en", "fr"] as const) {
      for (const code of codes) {
        const message = t(locale, `errors.retention.${code}`);
        expect(message, `${locale}:${code}`).not.toBe(`errors.retention.${code}`);
      }
      expect(t(locale, "errors.entitlements.packages")).not.toBe("errors.entitlements.packages");
      expect(t(locale, "errors.entitlements.memberships")).not.toBe("errors.entitlements.memberships");
      expect(t(locale, "errors.entitlements.recurringMembershipBilling")).not.toBe("errors.entitlements.recurringMembershipBilling");
    }
  });
  it("parses loyalty program, account and ledger rows and validates program / adjust requests", () => {
    const program = loyaltyProgramSchema.parse({
      configured: true, id, isEnabled: true, currency: "NGN", earnPoints: 1, earnPerAmount: 100, redeemPoints: 100,
      redeemValue: 500, minRedeemPoints: 100, pointsExpiryMonths: null, createdAt: "2026-10-29T12:00:00Z", updatedAt: "2026-10-29T12:00:00Z",
    });
    expect(program.pointsExpiryMonths).toBeNull();
    expect(loyaltyProgramSchema.safeParse({ ...program, configured: false, id: null, createdAt: null, updatedAt: null }).success).toBe(true);

    const body = {
      isEnabled: true, currency: "NGN", earnPoints: 1, earnPerAmount: 100, redeemPoints: 100, redeemValue: 500, minRedeemPoints: 100,
    };
    expect(upsertLoyaltyProgramRequestSchema.safeParse(body).success).toBe(true);
    expect(upsertLoyaltyProgramRequestSchema.safeParse({ ...body, pointsExpiryMonths: null }).success).toBe(true);
    expect(upsertLoyaltyProgramRequestSchema.safeParse({ ...body, earnPoints: 0 }).success).toBe(false);
    expect(upsertLoyaltyProgramRequestSchema.safeParse({ ...body, redeemValue: 0 }).success).toBe(false);
    expect(upsertLoyaltyProgramRequestSchema.safeParse({ ...body, pointsExpiryMonths: 61 }).success).toBe(false);

    expect(loyaltyAccountSchema.parse({
      tenantCustomerId: id, pointsBalance: 10, lifetimeEarned: 0, lifetimeRedeemed: 0, updatedAt: null,
    }).pointsBalance).toBe(10);
    const adjustment = loyaltyAdjustmentSchema.parse({
      account: { tenantCustomerId: id, pointsBalance: 120, lifetimeEarned: 0, lifetimeRedeemed: 0, updatedAt: "2026-10-29T12:00:00Z" },
      transaction: { id, sequence: 1, kind: "adjust", points: 120, balanceAfter: 120, reason: "Welcome", bookingId: null, occurredAt: "2026-10-29T12:00:00Z" },
    });
    expect(adjustment.transaction.kind).toBe("adjust");
    expect(loyaltyTransactionKindSchema.safeParse("transfer").success).toBe(false);

    expect(adjustLoyaltyPointsRequestSchema.safeParse({ points: -5, reason: "Fix" }).success).toBe(true);
    expect(adjustLoyaltyPointsRequestSchema.safeParse({ points: 0, reason: "Fix" }).success).toBe(false);
    expect(adjustLoyaltyPointsRequestSchema.safeParse({ points: 1.5, reason: "Fix" }).success).toBe(false);
    expect(adjustLoyaltyPointsRequestSchema.safeParse({ points: 1_000_001, reason: "Fix" }).success).toBe(false);
    expect(adjustLoyaltyPointsRequestSchema.safeParse({ points: 5, reason: "   " }).success).toBe(false);
  });

  it("has English and French messages for every loyalty error code and the portal page", () => {
    const codes = ["loyalty_program_not_configured", "loyalty_insufficient_points", "loyalty_conflict"];
    const pageKeys = [
      "title", "description", "setupNotice", "nonCash", "notConfigured", "enabled", "enabledHint", "currency", "earnHeading",
      "earnPoints", "earnPerAmount", "redeemHeading", "redeemPoints", "redeemValue", "minRedeem", "expiryHeading", "expiryMonths",
      "expiryNever", "summaryEarn", "summaryRedeem", "save", "saved", "couldNotLoad", "couldNotSave", "ownerOnlyHint",
    ];
    for (const locale of ["en", "fr"] as const) {
      for (const code of codes) {
        expect(t(locale, `errors.retention.${code}`), `${locale}:${code}`).not.toBe(`errors.retention.${code}`);
      }
      expect(t(locale, "errors.entitlements.loyaltyBasic")).not.toBe("errors.entitlements.loyaltyBasic");
      for (const key of pageKeys) {
        expect(t(locale, `businessApp.loyalty.${key}`), `${locale}:${key}`).not.toBe(`businessApp.loyalty.${key}`);
      }
    }
  });

  it("parses customer memberships and validates the enrol request", () => {
    const uuid = "11111111-1111-4111-8111-111111111111";
    const membership = customerMembershipSchema.parse({
      id: uuid, tenantId: uuid, tenantCustomerId: uuid, membershipPlanId: uuid, name: "Fade Club", price: 25000,
      currency: "NGN", billingInterval: "monthly", rolloverPolicy: "none", cancellationPolicy: "end_of_cycle",
      allowFreeze: true, pastDueGraceDays: 7, status: "active", enrolChannel: "in_person_till",
      enrolledAtLocationId: uuid, currentCycleNumber: 1, currentCycleStart: "2026-10-29T12:00:00Z",
      currentCycleEnd: "2026-11-29T12:00:00Z", activatedAt: "2026-10-29T12:00:00Z", frozenAt: null,
      cancelRequestedAt: null, cancelledAt: null, cancelReason: null, note: null,
      benefits: [{ serviceOfferingId: uuid, serviceName: "Haircut", quantityPerCycle: 2 }],
      balances: [{
        id: uuid, cycleNumber: 1, serviceOfferingId: uuid, serviceName: "Haircut", granted: 2, used: 0,
        reserved: 0, forfeited: 0, remaining: 2, expiresAt: "2026-11-29T12:00:00Z",
      }],
      createdAt: "2026-10-29T12:00:00Z",
    });
    expect(membership.status).toBe("active");
    expect(customerMembershipStatusSchema.safeParse("paused").success).toBe(false);

    expect(enrolMembershipRequestSchema.safeParse({ membershipPlanId: uuid, businessLocationId: uuid, paymentMethod: "till" }).success).toBe(true);
    expect(enrolMembershipRequestSchema.safeParse({ membershipPlanId: uuid, businessLocationId: uuid, paymentMethod: "card" }).success).toBe(false);
  });

  it("parses public membership plans, subscribe, and MRR, and accepts membership credits on booking", () => {
    const uuid = "11111111-1111-4111-8111-111111111111";
    const catalog = publicMembershipPlanCatalogSchema.parse({
      onlineSubscribeEnabled: false,
      items: [{
        id: uuid, name: "Fade Club", description: null, price: 25000, currency: "NGN",
        billingInterval: "monthly", rolloverPolicy: "none", cancellationPolicy: "end_of_cycle",
        allowFreeze: true, pastDueGraceDays: 7,
        benefits: [{ id: uuid, serviceOfferingId: uuid, serviceName: "Haircut", quantityPerCycle: 2 }],
      }],
    });
    expect(catalog.items).toHaveLength(1);
    expect(membershipSubscribeResponseSchema.parse({
      customerMembershipId: uuid, paymentIntentId: uuid, status: "pending", checkoutUrl: null,
    }).status).toBe("pending");
    expect(tenantMembershipMrrResponseSchema.parse({
      asOf: "2026-10-29T12:00:00Z",
      items: [{ currency: "NGN", monthlyRecurringRevenue: 25000, memberCount: 1 }],
    }).items[0]?.memberCount).toBe(1);
    expect(appliedBenefitRequestSchema.safeParse({ customerMembershipId: uuid, benefitBalanceId: uuid, quantity: 1 }).success).toBe(true);
    expect(appliedBenefitRequestSchema.safeParse({ benefitBalanceId: uuid, quantity: 1 }).success).toBe(false);
    expect(appliedBenefitRequestSchema.safeParse({
      customerPackageId: uuid, customerMembershipId: uuid, benefitBalanceId: uuid, quantity: 1,
    }).success).toBe(false);
  });
});
