import { redirect } from "next/navigation";
import {
  AdeniRoles,
  getRolesFromAuth0User,
  getTenantIdFromAuth0User,
  type AdeniRole,
} from "@adeni/shared";
import { getAuth0 } from "./auth0";
import { isAuth0Configured } from "./config";

export type WebSession = {
  user: Record<string, unknown>;
  name: string | null;
  email: string | null;
  roles: AdeniRole[];
  tenantId: string | null;
};

function toWebSession(user: Record<string, unknown>): WebSession {
  return {
    user,
    name: typeof user.name === "string" ? user.name : null,
    email: typeof user.email === "string" ? user.email : null,
    roles: getRolesFromAuth0User(user),
    tenantId: getTenantIdFromAuth0User(user),
  };
}

export async function getOptionalSession(): Promise<WebSession | null> {
  if (!isAuth0Configured()) {
    return null;
  }

  const session = await getAuth0().getSession();
  if (!session?.user) {
    return null;
  }

  return toWebSession(session.user as Record<string, unknown>);
}

export async function requireSession(returnTo: string): Promise<WebSession> {
  const session = await getOptionalSession();
  if (!session) {
    redirect(`/auth/login?returnTo=${encodeURIComponent(returnTo)}`);
  }

  return session;
}

export async function requireRole(
  role: AdeniRole,
  returnTo: string,
): Promise<WebSession> {
  const session = await requireSession(returnTo);

  if (!session.roles.includes(role) && !session.roles.includes(AdeniRoles.Admin)) {
    redirect("/forbidden");
  }

  return session;
}

export async function getAccessToken(): Promise<string | null> {
  if (!isAuth0Configured()) {
    return null;
  }

  try {
    const { token } = await getAuth0().getAccessToken();
    return token ?? null;
  } catch {
    return null;
  }
}
