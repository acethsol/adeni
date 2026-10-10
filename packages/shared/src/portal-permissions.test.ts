import { describe, expect, it } from "vitest";
import {
  defaultPermissionRoleForFloorRole,
  hasPortalPermission,
  normalizePortalPermissionRole,
  permissionsForRole,
} from "./portal-permissions";

describe("portal-permissions", () => {
  it("gives owners every permission", () => {
    const perms = permissionsForRole("owner");
    expect(perms).toContain("portal.plan");
    expect(perms).toContain("portal.staff");
    expect(hasPortalPermission(perms, "portal.plan")).toBe(true);
  });

  it("keeps billing owner-only for managers", () => {
    const perms = permissionsForRole("manager");
    expect(perms).toContain("portal.staff");
    expect(perms).not.toContain("portal.plan");
    expect(hasPortalPermission(perms, "portal.plan")).toBe(false);
  });

  it("scopes practitioners to self bookings", () => {
    const perms = permissionsForRole("practitioner");
    expect(perms).toContain("portal.bookings.self");
    expect(perms).not.toContain("portal.bookings");
    expect(perms).not.toContain("portal.staff");
    expect(hasPortalPermission(perms, ["portal.bookings", "portal.bookings.self"])).toBe(true);
  });

  it("normalizes unknown roles to owner", () => {
    expect(normalizePortalPermissionRole("nope")).toBe("owner");
    expect(normalizePortalPermissionRole(undefined)).toBe("owner");
  });

  it("maps floor roles to invite defaults", () => {
    expect(defaultPermissionRoleForFloorRole("barber")).toBe("practitioner");
    expect(defaultPermissionRoleForFloorRole("receptionist")).toBe("receptionist");
    expect(defaultPermissionRoleForFloorRole("manager")).toBe("manager");
    expect(defaultPermissionRoleForFloorRole("accountant")).toBe("accountant");
  });
});
