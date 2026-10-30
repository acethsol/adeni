import { describe, expect, it } from "vitest";
import { crumbsForCustomerUrl } from "./customer-app-breadcrumbs";

const tr = (key: string) => key;

describe("crumbsForCustomerUrl", () => {
  it("adds booking detail and intake crumbs", () => {
    const crumbs = crumbsForCustomerUrl(tr, "/app/bookings/abc-123/intake");
    expect(crumbs.map((c) => c.label)).toEqual([
      "customer.app.shell.groupMain",
      "customer.app.nav.bookings",
      "customer.app.breadcrumbs.bookingDetail",
      "intake.title",
    ]);
    expect(crumbs[1]?.path).toBe("/app/bookings");
    expect(crumbs[2]?.path).toBe("/app/bookings/abc-123");
  });

  it("adds open-form crumb under intake", () => {
    const crumbs = crumbsForCustomerUrl(tr, "/app/bookings/abc-123/intake/form-1");
    expect(crumbs.map((c) => c.label)).toEqual([
      "customer.app.shell.groupMain",
      "customer.app.nav.bookings",
      "customer.app.breadcrumbs.bookingDetail",
      "intake.title",
      "customer.app.breadcrumbs.consultationForm",
    ]);
    expect(crumbs[3]?.path).toBe("/app/bookings/abc-123/intake");
  });

  it("adds receipts crumbs under account", () => {
    const crumbs = crumbsForCustomerUrl(tr, "/app/receipts");
    expect(crumbs.map((c) => c.label)).toEqual([
      "customer.app.shell.groupAccount",
      "customer.app.receipts.title",
    ]);
  });

  it("adds booking detail only for booking page", () => {
    const crumbs = crumbsForCustomerUrl(tr, "/app/bookings/abc-123");
    expect(crumbs.at(-1)?.label).toBe("customer.app.breadcrumbs.bookingDetail");
    expect(crumbs.at(-1)?.path).toBeUndefined();
  });
});
