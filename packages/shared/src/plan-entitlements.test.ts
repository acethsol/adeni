import { describe, expect, it } from "vitest";
import { PLAN_COMPARISON } from "./subscription-tiers";
import { PLAN_ENTITLEMENTS } from "./plan-entitlements";

describe("plan-entitlements", () => {
  it("Starter inbox matches PLAN_COMPARISON", () => {
    expect(PLAN_ENTITLEMENTS.starter.messaging).toBe(true);
    const inboxRow = PLAN_COMPARISON.find((row) => row.label === "Adeni inbox");
    expect(inboxRow?.starter).toBe(true);
  });

  it("Professional practitioner cap is 10", () => {
    expect(PLAN_ENTITLEMENTS.professional.maxPractitioners).toBe(10);
  });
});
