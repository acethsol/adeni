import { crumbsForBusinessUrl, mergeBusinessDetailBreadcrumbExtras } from "./business-breadcrumbs";

const labels = {
  groupLabel: () => "Operations",
  navLabel: (item: { label: string }) => item.label,
  tabLabel: (id: string) => id,
  tr: (key: string) => key,
  fallbackGroupLabel: () => "Operations",
};

describe("crumbsForBusinessUrl", () => {
  it("links Customers when on a customer profile", () => {
    const crumbs = crumbsForBusinessUrl("/customers/abc-123", labels);
    expect(crumbs[1]?.label).toBe("Customers");
    expect(crumbs[1]?.path).toBe("/customers");
  });

  it("adds loyalty tab on customer profile query", () => {
    const crumbs = crumbsForBusinessUrl("/customers/abc-123?tab=loyalty", labels);
    expect(crumbs.at(-1)?.label).toBe("businessApp.loyalty.customerTab");
  });

  it("adds retention segment tab", () => {
    const crumbs = crumbsForBusinessUrl("/retention/loyalty", labels);
    expect(crumbs.map((c) => c.label)).toEqual(["Operations", "Retention", "loyalty"]);
  });

  it("adds intake leaf under bookings", () => {
    const crumbs = crumbsForBusinessUrl("/bookings/b-1/intake", labels);
    expect(crumbs.at(-1)?.label).toBe("intake.title");
  });

  it("places customer name before booking intake leaf", () => {
    const base = crumbsForBusinessUrl("/bookings/b-1/intake", labels);
    const merged = mergeBusinessDetailBreadcrumbExtras("/bookings/b-1/intake", base, [
      { label: "Dev Customer", path: "/customers/abc-123" },
    ]);
    expect(merged.map((c) => c.label)).toEqual([
      "Operations",
      "Bookings",
      "Dev Customer",
      "intake.title",
    ]);
  });

  it("places customer name before tab leaf", () => {
    const base = crumbsForBusinessUrl("/customers/abc-123?tab=bookings", labels);
    const merged = mergeBusinessDetailBreadcrumbExtras("/customers/abc-123?tab=bookings", base, [
      { label: "Dev Customer", path: "/customers/abc-123" },
    ]);
    expect(merged.map((c) => c.label)).toEqual([
      "Operations",
      "Customers",
      "Dev Customer",
      "common.bookings",
    ]);
  });
});
