import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import {
  buildPlanComparison,
  legacyAdvancedReminders,
  PLAN_COMPARISON,
  PLAN_COMPARISON_ROW_DEFS,
  PLAN_ENTITLEMENTS,
} from "./plan-entitlements";

const repoSeedPath = join(
  dirname(fileURLToPath(import.meta.url)),
  "../../../docs/specs/adeni-subscription-pricing-v1/plan-entitlements.seed.json",
);

describe("plan-entitlements", () => {
  it("Starter inbox is enabled", () => {
    expect(PLAN_ENTITLEMENTS.starter.messaging).toBe(true);
    const inboxRow = PLAN_COMPARISON.find((row) => row.label === "Adeni messaging");
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

  it("shared catalog matches docs seed file", () => {
    const seed = JSON.parse(readFileSync(repoSeedPath, "utf8"));
    expect(PLAN_ENTITLEMENTS).toEqual(seed);
  });

  it("PLAN_COMPARISON omits table-stakes finance rows", () => {
    const labels = PLAN_COMPARISON.map((row) => row.label);
    expect(labels).not.toContain("Deposits");
    expect(labels).not.toContain("Online payments");
    expect(labels).not.toContain("Invoices & receipts");
    expect(labels).not.toContain("Basic analytics");
  });

  it("PLAN_COMPARISON omits deferred module rows until enforced", () => {
    const labels = PLAN_COMPARISON.map((row) => row.label);
    expect(labels).not.toContain("Basic reminders");
    expect(labels).not.toContain("Configurable reminders");
    expect(labels).not.toContain("Follow-up automation");
    expect(labels).not.toContain("Basic exports");
    expect(labels).not.toContain("Advanced reports");
    expect(labels).not.toContain("Excel export");
    expect(labels).not.toContain("Multi-location reporting");
  });
});
