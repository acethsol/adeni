/**
 * Portal RBAC — what a business login can open after auth.
 * Distinct from floor `roleKey` on StaffMember (booking label).
 * Spec: docs/specs/sprint-25-staff-accounts-permissions.md
 */

export const PORTAL_PERMISSIONS = [
  "portal.overview",
  "portal.bookings",
  "portal.bookings.self",
  "portal.messages",
  "portal.quotes",
  "portal.services",
  "portal.staff",
  "portal.staff.self",
  "portal.hours",
  "portal.locations",
  "portal.public_page",
  "portal.profile",
  "portal.payments",
  "portal.plan",
] as const;

export type PortalPermission = (typeof PORTAL_PERMISSIONS)[number];

export const PORTAL_PERMISSION_ROLES = [
  "owner",
  "manager",
  "receptionist",
  "practitioner",
  "accountant",
  "ops",
] as const;

export type PortalPermissionRole = (typeof PORTAL_PERMISSION_ROLES)[number];

const ALL: readonly PortalPermission[] = PORTAL_PERMISSIONS;

const MANAGER: readonly PortalPermission[] = PORTAL_PERMISSIONS.filter((p) => p !== "portal.plan");

const RECEPTIONIST: readonly PortalPermission[] = [
  "portal.overview",
  "portal.bookings",
  "portal.messages",
  "portal.quotes",
  "portal.services",
  "portal.hours",
];

const PRACTITIONER: readonly PortalPermission[] = [
  "portal.overview",
  "portal.bookings.self",
  "portal.staff.self",
  "portal.messages",
];

const ACCOUNTANT: readonly PortalPermission[] = [
  "portal.overview",
  "portal.payments",
];

const OPS: readonly PortalPermission[] = [
  "portal.overview",
  "portal.services",
  "portal.hours",
  "portal.locations",
  "portal.public_page",
];

export const PORTAL_PERMISSION_ROLE_TEMPLATES: Record<
  PortalPermissionRole,
  readonly PortalPermission[]
> = {
  owner: ALL,
  manager: MANAGER,
  receptionist: RECEPTIONIST,
  practitioner: PRACTITIONER,
  accountant: ACCOUNTANT,
  ops: OPS,
};

export function isPortalPermissionRole(value: string | null | undefined): value is PortalPermissionRole {
  return !!value && (PORTAL_PERMISSION_ROLES as readonly string[]).includes(value);
}

export function normalizePortalPermissionRole(
  value: string | null | undefined,
): PortalPermissionRole {
  const key = value?.trim().toLowerCase() ?? "";
  return isPortalPermissionRole(key) ? key : "owner";
}

export function permissionsForRole(role: string | null | undefined): readonly PortalPermission[] {
  const normalized = normalizePortalPermissionRole(role);
  return PORTAL_PERMISSION_ROLE_TEMPLATES[normalized];
}

export function hasPortalPermission(
  permissions: readonly string[] | undefined,
  required: PortalPermission | readonly PortalPermission[],
): boolean {
  if (!permissions?.length) {
    return false;
  }
  const need = typeof required === "string" ? [required] : required;
  return need.some((key) => permissions.includes(key));
}

/** Floor roleKey → default portal RBAC role when inviting (Sprint 25b). */
export function defaultPermissionRoleForFloorRole(roleKey: string | null | undefined): PortalPermissionRole {
  switch ((roleKey ?? "").trim().toLowerCase()) {
    case "receptionist":
      return "receptionist";
    case "manager":
    case "supervisor":
      return "manager";
    case "accountant":
      return "accountant";
    case "inventory_manager":
    case "marketing":
    case "hr":
    case "admin_staff":
    case "other":
      return "ops";
    default:
      return "practitioner";
  }
}
