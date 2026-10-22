import { describe, expect, it } from "vitest";
import { PLAN_ENTITLEMENTS } from "./plan-entitlements";
import type { BusinessLocation } from "./schemas";
import {
  activeBusinessLocations,
  needsReportExportLocationPicker,
  resolveReportExportLocationId,
} from "./report-export-location";

function loc(partial: Partial<BusinessLocation> & Pick<BusinessLocation, "id">): BusinessLocation {
  return {
    slug: "branch",
    name: "Branch",
    addressLine: "1 St",
    area: "Area",
    marketId: "lagos",
    isPrimary: false,
    isActive: true,
    ...partial,
  };
}

describe("report export location", () => {
  it("auto-passes the only active location", () => {
    const locations = [loc({ id: "a", isPrimary: true })];
    expect(
      resolveReportExportLocationId({
        locations,
        entitlements: PLAN_ENTITLEMENTS.starter,
      }),
    ).toBe("a");
    expect(needsReportExportLocationPicker(locations, PLAN_ENTITLEMENTS.starter)).toBe(false);
  });

  it("defaults to primary branch when multi-location gate applies", () => {
    const locations = [
      loc({ id: "primary", isPrimary: true }),
      loc({ id: "secondary", isPrimary: false }),
    ];
    expect(needsReportExportLocationPicker(locations, PLAN_ENTITLEMENTS.professional)).toBe(true);
    expect(
      resolveReportExportLocationId({
        locations,
        entitlements: PLAN_ENTITLEMENTS.professional,
      }),
    ).toBe("primary");
  });

  it("honors selected branch and skips picker when Business entitlement allows all locations", () => {
    const locations = [
      loc({ id: "primary", isPrimary: true }),
      loc({ id: "secondary", isPrimary: false }),
    ];
    expect(needsReportExportLocationPicker(locations, PLAN_ENTITLEMENTS.business)).toBe(false);
    expect(
      resolveReportExportLocationId({
        locations,
        entitlements: PLAN_ENTITLEMENTS.business,
        selectedLocationId: "secondary",
      }),
    ).toBe("secondary");
    expect(
      resolveReportExportLocationId({
        locations,
        entitlements: PLAN_ENTITLEMENTS.business,
      }),
    ).toBeUndefined();
  });

  it("filters inactive locations", () => {
    const locations = [
      loc({ id: "closed", isActive: false }),
      loc({ id: "open", isPrimary: true }),
    ];
    expect(activeBusinessLocations(locations).map((l) => l.id)).toEqual(["open"]);
  });
});
