import { describe, expect, it } from "vitest";
import { mergeTenantEntitlements } from "./entitlements";
import { PLAN_ENTITLEMENTS } from "./plan-entitlements";
import {
  buildAdminReportExportMenuItems,
  buildReportExportMenuItems,
  salesSummaryPdfMenuOption,
} from "./report-export-menu";

const tenantPlans = {
  starter: { ...PLAN_ENTITLEMENTS.starter, multiLocation: false },
  professional: { ...PLAN_ENTITLEMENTS.professional, multiLocation: false },
  business: { ...PLAN_ENTITLEMENTS.business, multiLocation: true },
};

describe("buildReportExportMenuItems", () => {
  it("enables CSV for starter entitlements and omits PDF when not requested", () => {
    const items = buildReportExportMenuItems({
      entitlements: tenantPlans.starter,
      xlsx: true,
    });
    expect(items.map((i) => i.format)).toEqual(["csv", "xlsx"]);
    const csv = items.find((i) => i.format === "csv");
    const xlsx = items.find((i) => i.format === "xlsx");
    expect(csv?.disabled).toBe(false);
    expect(csv?.hint).toBeUndefined();
    expect(xlsx?.disabled).toBe(true);
    expect(xlsx?.hint).toBe("Pro plan required for Excel");
    for (const item of items) {
      expect(item.hint ?? "").not.toMatch(/coming soon/i);
    }
  });

  it("enables CSV and Excel for professional entitlements", () => {
    const items = buildReportExportMenuItems({
      entitlements: tenantPlans.professional,
      xlsx: true,
    });
    expect(items.every((i) => !i.disabled)).toBe(true);
  });
});

describe("salesSummaryPdfMenuOption", () => {
  it("requires Pro for PDF when CSV tier is entitled", () => {
    expect(salesSummaryPdfMenuOption(tenantPlans.starter)).toEqual({
      disabled: true,
      hint: "Pro plan required for sales PDF",
    });
    expect(salesSummaryPdfMenuOption(tenantPlans.professional)).toEqual({
      disabled: false,
    });
  });
});

describe("compensation-style export menu", () => {
  it("requires Pro for CSV when csvRequiresAdvancedReports is set", () => {
    const starterItems = buildReportExportMenuItems({
      entitlements: tenantPlans.starter,
      xlsx: true,
      csvRequiresAdvancedReports: true,
    });
    expect(starterItems.find((i) => i.format === "csv")?.disabled).toBe(true);
    const proItems = buildReportExportMenuItems({
      entitlements: tenantPlans.professional,
      xlsx: true,
      csvRequiresAdvancedReports: true,
    });
    expect(proItems.find((i) => i.format === "csv")?.disabled).toBe(false);
    expect(proItems.find((i) => i.format === "xlsx")?.disabled).toBe(false);
  });
});

describe("buildAdminReportExportMenuItems", () => {
  it("enables CSV and Excel without PDF", () => {
    const items = buildAdminReportExportMenuItems({ xlsx: true });
    expect(items.map((i) => i.format)).toEqual(["csv", "xlsx"]);
    expect(items.every((i) => !i.disabled)).toBe(true);
  });
});

describe("mergeTenantEntitlements", () => {
  it("ORs excelExport from plan and usage when profile omits Excel", () => {
    const merged = mergeTenantEntitlements(
      tenantPlans.professional,
      { ...tenantPlans.starter, excelExport: false },
      { ...tenantPlans.starter, excelExport: undefined },
    );
    expect(merged?.excelExport).toBe(true);
    expect(merged?.basicExports).not.toBe(false);
  });
});
