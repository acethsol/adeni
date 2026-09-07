import { NextResponse, type NextRequest } from "next/server";
import {
  AdeniRoles,
  getRolesFromAuth0User,
  type AdeniRole,
} from "@adeni/shared";
import { isProductionDeployment } from "../env";
import { getAuth0 } from "./auth0";
import { isAuth0Configured } from "./config";

function loginRedirect(request: NextRequest, returnTo: string) {
  const loginUrl = new URL("/auth/login", request.url);
  loginUrl.searchParams.set("returnTo", returnTo);
  return NextResponse.redirect(loginUrl);
}

function userHasPortalRole(roles: AdeniRole[], required: AdeniRole): boolean {
  return roles.includes(required) || roles.includes(AdeniRoles.Admin);
}

export async function runAuthMiddleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  if (
    isProductionDeployment() &&
    !isAuth0Configured() &&
    (pathname.startsWith("/business") || pathname.startsWith("/admin"))
  ) {
    const setupUrl = new URL("/auth/setup-required", request.url);
    setupUrl.searchParams.set("returnTo", pathname);
    return NextResponse.redirect(setupUrl);
  }

  if (!isAuth0Configured()) {
    return NextResponse.next();
  }

  const authResponse = await getAuth0().middleware(request);

  if (pathname.startsWith("/auth")) {
    return authResponse;
  }

  const requiresBusiness = pathname.startsWith("/business");
  const requiresAdmin = pathname.startsWith("/admin");

  if (!requiresBusiness && !requiresAdmin) {
    return authResponse;
  }

  const session = await getAuth0().getSession(request);
  if (!session?.user) {
    return loginRedirect(request, pathname);
  }

  const roles = getRolesFromAuth0User(session.user as Record<string, unknown>);

  if (requiresAdmin && !userHasPortalRole(roles, AdeniRoles.Admin)) {
    return NextResponse.redirect(new URL("/forbidden", request.url));
  }

  if (requiresBusiness && !userHasPortalRole(roles, AdeniRoles.Business)) {
    return NextResponse.redirect(new URL("/forbidden", request.url));
  }

  return authResponse;
}
