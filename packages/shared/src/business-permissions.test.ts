import { describe, expect, it } from "vitest";
import {
  defaultPermissionRoleForFloorRole,
  hasBusinessPermission,
  normalizeBusinessPermissionRole,
  permissionsForRole,
} from "./business-permissions";

describe("business-permissions", () => {
  it("gives owners every permission", () => {
    const perms = permissionsForRole("owner");
    expect(perms).toContain("business.plan");
    expect(perms).toContain("business.staff");
    expect(hasBusinessPermission(perms, "business.plan")).toBe(true);
  });

  it("keeps billing owner-only for managers", () => {
    const perms = permissionsForRole("manager");
    expect(perms).toContain("business.staff");
    expect(perms).not.toContain("business.plan");
    expect(hasBusinessPermission(perms, "business.plan")).toBe(false);
  });

  it("scopes practitioners to self bookings", () => {
    const perms = permissionsForRole("practitioner");
    expect(perms).toContain("business.bookings.self");
    expect(perms).not.toContain("business.bookings");
    expect(perms).not.toContain("business.staff");
    expect(hasBusinessPermission(perms, ["business.bookings", "business.bookings.self"])).toBe(true);
  });

  it("normalizes unknown roles to owner", () => {
    expect(normalizeBusinessPermissionRole("nope")).toBe("owner");
    expect(normalizeBusinessPermissionRole(undefined)).toBe("owner");
  });

  it("maps floor roles to invite defaults", () => {
    expect(defaultPermissionRoleForFloorRole("barber")).toBe("practitioner");
    expect(defaultPermissionRoleForFloorRole("receptionist")).toBe("receptionist");
    expect(defaultPermissionRoleForFloorRole("manager")).toBe("manager");
    expect(defaultPermissionRoleForFloorRole("accountant")).toBe("accountant");
  });
});
