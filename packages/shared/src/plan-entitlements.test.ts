import { describe, expect, it } from "vitest";
import {
  buildPlanComparison,
  legacyAdvancedReminders,
  PLAN_COMPARISON,
  PLAN_COMPARISON_ROW_DEFS,
  PLAN_ENTITLEMENTS,
} from "./plan-entitlements";

describe("plan-entitlements", () => {
  it("Starter inbox is enabled", () => {
    expect(PLAN_ENTITLEMENTS.starter.messaging).toBe(true);
    const inboxRow = PLAN_COMPARISON.find((row) => row.label === "Adeni inbox");
    expect(inboxRow?.starter).toBe(true);
  });

  it("Professional practitioner cap is 10", () => {
    expect(PLAN_ENTITLEMENTS.professional.maxPractitioners).toBe(10);
  });

  it("Business tier uses 3 locations and 25 practitioners (not unlimited)", () => {
    expect(PLAN_ENTITLEMENTS.business.maxLocations).toBe(3);
    expect(PLAN_ENTITLEMENTS.business.maxPractitioners).toBe(25);
    expect(PLAN_ENTITLEMENTS.business.additionalLocationsAllowed).toBe(true);
    expect(PLAN_ENTITLEMENTS.business.additionalPractitionersAllowed).toBe(true);
  });

  it("Starter has basic reminders only", () => {
    expect(PLAN_ENTITLEMENTS.starter.basicReminders).toBe(true);
    expect(PLAN_ENTITLEMENTS.starter.configurableReminders).toBe(false);
    expect(PLAN_ENTITLEMENTS.starter.followUpAutomation).toBe(false);
    expect(legacyAdvancedReminders(PLAN_ENTITLEMENTS.starter)).toBe(false);
  });

  it("Pro and business include SMS with usage billing", () => {
    expect(PLAN_ENTITLEMENTS.starter.smsEnabled).toBe(false);
    expect(PLAN_ENTITLEMENTS.starter.smsUsageBilling).toBe(false);
    expect(PLAN_ENTITLEMENTS.professional.smsEnabled).toBe(true);
    expect(PLAN_ENTITLEMENTS.professional.smsUsageBilling).toBe(true);
    expect(PLAN_ENTITLEMENTS.business.smsEnabled).toBe(true);
    expect(PLAN_ENTITLEMENTS.business.smsUsageBilling).toBe(true);
  });

  it("PLAN_COMPARISON stays aligned with seed matrix", () => {
    expect(PLAN_COMPARISON).toEqual(buildPlanComparison());
    expect(PLAN_COMPARISON.length).toBe(PLAN_COMPARISON_ROW_DEFS.length);
  });
});
