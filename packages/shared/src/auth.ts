import { AdeniRoles, type AdeniRole } from "./roles";

export const AdeniClaims = {
  roles: "https://adeni.io/roles",
  tenantId: "https://adeni.io/tenant_id",
  platformUserId: "https://adeni.io/platform_user_id",
} as const;

const roleValues = new Set<string>(Object.values(AdeniRoles));

function isAdeniRole(value: string): value is AdeniRole {
  return roleValues.has(value);
}

export function getRolesFromAuth0User(
  user: Record<string, unknown> | null | undefined,
): AdeniRole[] {
  if (!user) {
    return [];
  }

  const raw = user[AdeniClaims.roles];
  const values = Array.isArray(raw) ? raw : typeof raw === "string" ? [raw] : [];

  return values
    .filter((value): value is string => typeof value === "string")
    .filter(isAdeniRole);
}

export function userHasRole(
  user: Record<string, unknown> | null | undefined,
  role: AdeniRole,
): boolean {
  return getRolesFromAuth0User(user).includes(role);
}

export function getTenantIdFromAuth0User(
  user: Record<string, unknown> | null | undefined,
): string | null {
  if (!user) {
    return null;
  }

  const raw = user[AdeniClaims.tenantId];
  return typeof raw === "string" ? raw : null;
}
