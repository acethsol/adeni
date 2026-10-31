import { crumbsForAdminUrl } from "./admin-breadcrumbs";

const labels = {
  groupLabel: () => "Platform",
  navLabel: (item: { path: string; label: string }) =>
    item.path === "/pricing/catalog" ? "Plans & pricing" : item.label,
  tr: (key: string) => key,
  chrome: (key: string) => key,
};

describe("crumbsForAdminUrl", () => {
  it("builds pricing sub-trail for GMV", () => {
    const crumbs = crumbsForAdminUrl("/pricing/gmv", labels);
    expect(crumbs.map((c) => c.label)).toEqual([
      "Platform",
      "Plans & pricing",
      "admin.breadcrumbs.gmvFees",
    ]);
    expect(crumbs[1]?.path).toBe("/pricing/catalog");
  });

  it("links Businesses list on tenant detail prefix", () => {
    const crumbs = crumbsForAdminUrl("/businesses/t-1/overview", labels);
    expect(crumbs.map((c) => c.label)).toEqual(["Platform", "Businesses"]);
    expect(crumbs[1]?.path).toBe("/businesses");
  });
});
