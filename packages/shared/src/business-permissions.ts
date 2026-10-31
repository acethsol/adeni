/**
 * Business app RBAC — what a business login can open after auth.
 * Distinct from floor `roleKey` on StaffMember (booking label).
 * Spec: docs/specs/sprint-25-staff-accounts-permissions.md
 */

export const BUSINESS_PERMISSIONS = [
  "business.overview",
  "business.bookings",
  "business.bookings.self",
  "business.messages",
  "business.quotes",
  "business.services",
  "business.staff",
  "business.staff.self",
  "business.hours",
  "business.locations",
  "business.public_page",
  "business.profile",
  "business.finance",
  "business.plan",
  "business.retention",
  "business.attendance",
  "business.attendance.self",
  "business.attendance.manage",
] as const;

export type BusinessPermission = (typeof BUSINESS_PERMISSIONS)[number];

export const BUSINESS_PERMISSION_ROLES = [
  "owner",
  "manager",
  "receptionist",
  "practitioner",
  "accountant",
  "ops",
] as const;

export type BusinessPermissionRole = (typeof BUSINESS_PERMISSION_ROLES)[number];

const ALL: readonly BusinessPermission[] = BUSINESS_PERMISSIONS;

const MANAGER: readonly BusinessPermission[] = BUSINESS_PERMISSIONS.filter((p) => p !== "business.plan");

const RECEPTIONIST: readonly BusinessPermission[] = [
  "business.overview",
  "business.bookings",
  "business.messages",
  "business.quotes",
  "business.services",
  "business.hours",
  "business.retention",
  "business.attendance.self",
  "business.attendance",
];

const PRACTITIONER: readonly BusinessPermission[] = [
  "business.overview",
  "business.bookings.self",
  "business.staff.self",
  "business.messages",
  "business.attendance.self",
];

const ACCOUNTANT: readonly BusinessPermission[] = [
  "business.overview",
  "business.finance",
];

const OPS: readonly BusinessPermission[] = [
  "business.overview",
  "business.services",
  "business.hours",
  "business.locations",
  "business.public_page",
];

export const BUSINESS_PERMISSION_ROLE_TEMPLATES: Record<
  BusinessPermissionRole,
  readonly BusinessPermission[]
> = {
  owner: ALL,
  manager: MANAGER,
  receptionist: RECEPTIONIST,
  practitioner: PRACTITIONER,
  accountant: ACCOUNTANT,
  ops: OPS,
};

export function isBusinessPermissionRole(value: string | null | undefined): value is BusinessPermissionRole {
  return !!value && (BUSINESS_PERMISSION_ROLES as readonly string[]).includes(value);
}

export function normalizeBusinessPermissionRole(
  value: string | null | undefined,
): BusinessPermissionRole {
  const key = value?.trim().toLowerCase() ?? "";
  return isBusinessPermissionRole(key) ? key : "owner";
}

export function permissionsForRole(role: string | null | undefined): readonly BusinessPermission[] {
  const normalized = normalizeBusinessPermissionRole(role);
  return BUSINESS_PERMISSION_ROLE_TEMPLATES[normalized];
}

export function hasBusinessPermission(
  permissions: readonly string[] | undefined,
  required: BusinessPermission | readonly BusinessPermission[],
): boolean {
  if (!permissions?.length) {
    return false;
  }
  const need = typeof required === "string" ? [required] : required;
  return need.some((key) => permissions.includes(key));
}

/** Floor roleKey → default portal RBAC role when inviting (Sprint 25b). */
export function defaultPermissionRoleForFloorRole(roleKey: string | null | undefined): BusinessPermissionRole {
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
