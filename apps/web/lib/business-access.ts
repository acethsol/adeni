import { AdeniRoles } from "@adeni/shared";
import { isAuth0Configured } from "./auth/config";
import { requireRole, type WebSession } from "./auth/session";
import { isBusinessPortalDevMode } from "./business-api";

export type BusinessPortalAccess = {
  mode: "auth0" | "dev";
  session: WebSession | null;
};

export async function requireBusinessPortalAccess(
  returnTo: string,
): Promise<BusinessPortalAccess> {
  if (isAuth0Configured()) {
    const session = await requireRole(AdeniRoles.Business, returnTo);
    return { mode: "auth0", session };
  }

  if (isBusinessPortalDevMode()) {
    return { mode: "dev", session: null };
  }

  throw new Error("Business portal requires Auth0 or DEV_BUSINESS_AUTH0_SUB.");
}

export function canAccessBusinessPortal(): boolean {
  return isAuth0Configured() || isBusinessPortalDevMode();
}
