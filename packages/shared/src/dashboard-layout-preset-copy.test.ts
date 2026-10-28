import { describe, expect, it } from "vitest";
import { resolveRecommendedDashboardLayoutPresetId } from "./dashboard-layout-preset-copy";
import { dashboardLayoutFingerprint, visibleDashboardLayoutFingerprint } from "./dashboard-layout";
import { resolveDashboardLayoutPreset } from "./dashboard-layout-presets";
import type { DashboardWidgetVisibilityContext } from "./dashboard-layout";

const starterEntitlements = {
  maxLocations: 1,
  maxPractitioners: 3,
  messaging: false,
  waitlist: false,
  advancedAnalytics: false,
  customerRetentionAnalytics: false,
  staffPerformanceAnalytics: false,
  multiLocationReporting: false,
  prioritySupport: false,
  packages: false,
  multiLocation: false,
} as const;

function ctx(
  partial: Partial<DashboardWidgetVisibilityContext> = {},
): DashboardWidgetVisibilityContext {
  return {
    isPractitionerView: false,
    entitlements: starterEntitlements,
    capabilities: ["calendar"],
    canPortal: () => true,
    ...partial,
  };
}

describe("resolveRecommendedDashboardLayoutPresetId", () => {
  it("maps portal roles to preset ids", () => {
    expect(
      resolveRecommendedDashboardLayoutPresetId({
        isPractitionerView: false,
        permissionRole: "receptionist",
      }),
    ).toBe("front_desk");
    expect(
      resolveRecommendedDashboardLayoutPresetId({
        isPractitionerView: true,
        permissionRole: "owner",
      }),
    ).toBe("practitioner");
    expect(
      resolveRecommendedDashboardLayoutPresetId({
        isPractitionerView: false,
        permissionRole: "accountant",
      }),
    ).toBe("minimal");
  });
});

describe("dashboardLayoutFingerprint", () => {
  it("matches filtered preset layout after apply", () => {
    const preset = resolveDashboardLayoutPreset("minimal");
    const fingerprint = visibleDashboardLayoutFingerprint(preset, ctx());
    expect(fingerprint).toContain("today");
    expect(fingerprint).toContain("kpis_row:12");
    expect(dashboardLayoutFingerprint(preset)).toBe(fingerprint);
  });
});
