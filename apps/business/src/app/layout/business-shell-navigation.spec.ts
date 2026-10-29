import { computed, signal } from "@angular/core";
import { businessProfileSchema, permissionsForRole, PLAN_ENTITLEMENTS, tenantEntitlementsSchema, type BusinessPermission } from "@adeni/shared";
import { CHROME } from "./business-shell-copy";
import { businessNavigationGroups, businessSearchResults } from "./business-shell-navigation";

describe("Portal shell navigation", () => {
  const entitlements = tenantEntitlementsSchema.parse({ ...PLAN_ENTITLEMENTS.business, multiLocation: true });
  const profile = businessProfileSchema.parse({ tenantId: "tenant", businessName: "Shop", categorySlug: "barbers", status: 2,
    phone: "", description: "", createdAt: "2026-01-01", locations: [], verificationDocuments: [], entitlements });

  it("keeps permission, capability and entitlement navigation gates separate and reactive", () => {
    const permissions = signal<readonly BusinessPermission[]>(permissionsForRole("owner"));
    const groups = computed(() => businessNavigationGroups({ profile, profileLoaded: true, permissions: permissions() }));
    const paths = () => groups().flatMap(group => group.items.map(item => item.path));
    expect(paths()).toContain("/services");
    expect(paths()).not.toContain("/quotes");
    permissions.set([]);
    expect(paths()).not.toContain("/services");
    expect(paths()).not.toContain("/finance");
    expect(profile.entitlements?.messaging).toBeTrue();
  });

  it("preserves tab-first search ordering, translations and entitlement filtering", () => {
    const groups = businessNavigationGroups({ profile, profileLoaded: true, permissions: permissionsForRole("owner") });
    const options = { query: "messaging", groups, entitlements: { ...entitlements, messaging: false },
      navLabel: (item: { label: string }) => item.label, tabLabel: (id: string) => id,
      discoverLabel: CHROME.en["/__discover"], discoverUrl: "https://discover.example" };
    expect(businessSearchResults(options).some(hit => hit.path === "/settings?tab=messaging")).toBeFalse();
    expect(businessSearchResults({ ...options, entitlements }).some(hit => hit.path === "/settings?tab=messaging")).toBeTrue();
    const hits = businessSearchResults({ ...options, query: "", entitlements });
    expect(hits.length).toBeLessThanOrEqual(10);
    expect(new Set(hits.map(hit => hit.path)).size).toBe(hits.length);
    expect(hits[0].path).toContain("?tab=");
  });

  it("preserves the external Discover result and the existing locale copy", () => {
    const hits = businessSearchResults({ query: "discover", groups: [], entitlements: null,
      navLabel: item => item.label, tabLabel: id => id, discoverLabel: CHROME.fr["/__discover"], discoverUrl: "https://discover.example" });
    expect(hits).toEqual([{ id: "discover", label: "Découvrir", path: "https://discover.example", icon: "launch", external: true }]);
    expect(CHROME.en["/dashboard"]).toBe("Dashboard");
  });
});
