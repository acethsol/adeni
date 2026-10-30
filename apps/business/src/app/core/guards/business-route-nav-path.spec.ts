import type { ActivatedRouteSnapshot, UrlSegment } from "@angular/router";
import { businessRouteNavPath, retentionTabIdFromPath } from "./business-route-nav-path";

function routeWithPaths(...pathGroups: string[][]): ActivatedRouteSnapshot {
  let leaf: ActivatedRouteSnapshot | undefined;
  for (const segments of pathGroups) {
    leaf = {
      url: segments.map((path) => ({ path }) as UrlSegment),
      parent: leaf,
    } as ActivatedRouteSnapshot;
  }
  return leaf!;
}

describe("businessRouteNavPath", () => {
  it("joins parent and child segments with a leading slash", () => {
    const route = routeWithPaths(["retention"], ["memberships"]);
    expect(businessRouteNavPath(route)).toBe("/retention/memberships");
  });

  it("returns / for an empty route tree", () => {
    const route = { url: [], parent: undefined } as unknown as ActivatedRouteSnapshot;
    expect(businessRouteNavPath(route)).toBe("/");
  });
});

describe("retentionTabIdFromPath", () => {
  it("maps retention child paths to tab ids", () => {
    expect(retentionTabIdFromPath("/retention/packages")).toBe("packages");
    expect(retentionTabIdFromPath("/retention/memberships/extra")).toBe("memberships");
    expect(retentionTabIdFromPath("/retention/loyalty")).toBe("loyalty");
  });

  it("returns null for non-retention paths", () => {
    expect(retentionTabIdFromPath("/dashboard")).toBeNull();
    expect(retentionTabIdFromPath("/retention")).toBeNull();
  });
});
