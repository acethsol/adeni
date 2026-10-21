import { describe, expect, it } from "vitest";
import {
  formatDashboardRangeDateSpan,
  formatDashboardRangeOverviewHint,
  type DashboardRangePreset,
} from "./dashboard-range";

const fixedNow = new Date("2026-03-21T15:30:00.000Z");

describe("formatDashboardRangeDateSpan", () => {
  it("formats today with weekday", () => {
    expect(formatDashboardRangeDateSpan("today", fixedNow)).toMatch(/Saturday, Mar\.? 21/);
  });

  it("formats 7d as inclusive span", () => {
    expect(formatDashboardRangeDateSpan("7d", fixedNow)).toBe("Mar 15–21");
  });

  it("formats mtd from first of month", () => {
    expect(formatDashboardRangeDateSpan("mtd", fixedNow)).toBe("Mar 1–21");
  });
});

describe("formatDashboardRangeOverviewHint", () => {
  it("includes preset label and span", () => {
    expect(formatDashboardRangeOverviewHint("7d", fixedNow)).toBe("7 days · Mar 15–21");
    expect(formatDashboardRangeOverviewHint("mtd", fixedNow)).toBe("Month to date · Mar 1–21");
  });
});
