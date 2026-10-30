import { describe, expect, it } from "vitest";
import {
  CUSTOMER_APP_HOME,
  CUSTOMER_DESKTOP_NAV,
  CUSTOMER_NAV,
  CUSTOMER_TAB_NAV,
} from "./customer-nav";

describe("customer nav (C2)", () => {
  it("defaults Open app / post-login to dashboard", () => {
    expect(CUSTOMER_APP_HOME).toBe("/app/dashboard");
  });

  it("freezes dashboard + calendar ids and drops home", () => {
    const ids = CUSTOMER_NAV.map((item) => item.id);
    expect(ids).toContain("dashboard");
    expect(ids).toContain("calendar");
    expect(ids).not.toContain("home");
  });

  it("uses the locked five mobile tabs", () => {
    expect(CUSTOMER_TAB_NAV.map((item) => item.id)).toEqual([
      "dashboard",
      "calendar",
      "discover",
      "messages",
      "profile",
    ]);
  });

  it("keeps bookings and benefits on the desktop menu only", () => {
    const desktop = CUSTOMER_DESKTOP_NAV.map((item) => item.id);
    expect(desktop).toContain("bookings");
    expect(desktop).toContain("benefits");
    expect(CUSTOMER_TAB_NAV.some((item) => item.id === "bookings")).toBe(false);
    expect(CUSTOMER_TAB_NAV.some((item) => item.id === "benefits")).toBe(false);
  });
});
