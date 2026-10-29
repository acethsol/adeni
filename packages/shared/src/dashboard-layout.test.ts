import { describe, expect, it } from "vitest";
import {
  DEFAULT_OWNER_DASHBOARD_LAYOUT,
  filterDashboardLayoutByVisibility,
  isDashboardWidgetVisible,
  type DashboardWidgetVisibilityContext,
} from "./dashboard-layout";

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
  memberships: false,
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

describe("isDashboardWidgetVisible", () => {
  it("hides advanced analytics widgets without advancedAnalytics", () => {
    expect(isDashboardWidgetVisible("service_mix", ctx())).toBe(false);
    expect(isDashboardWidgetVisible("booked_value", ctx())).toBe(false);
    expect(
      isDashboardWidgetVisible(
        "service_mix",
        ctx({ entitlements: { ...starterEntitlements, advancedAnalytics: true } }),
      ),
    ).toBe(true);
  });

  it("hides staff leaderboard without staffPerformanceAnalytics", () => {
    expect(
      isDashboardWidgetVisible(
        "staff_leaderboard",
        ctx({ entitlements: { ...starterEntitlements, advancedAnalytics: true } }),
      ),
    ).toBe(false);
  });

  it("hides collected revenue without deposits capability", () => {
    expect(
      isDashboardWidgetVisible(
        "collected_revenue",
        ctx({ entitlements: { ...starterEntitlements, advancedAnalytics: true } }),
      ),
    ).toBe(false);
  });
});

describe("filterDashboardLayoutByVisibility", () => {
  it("strips widgets not on the tenant plan", () => {
    const filtered = filterDashboardLayoutByVisibility(DEFAULT_OWNER_DASHBOARD_LAYOUT, ctx());
    expect(filtered.widgets.some((entry) => entry.id === "service_mix")).toBe(false);
    expect(filtered.widgets.some((entry) => entry.id === "week_appointments")).toBe(true);
  });
});
