import { NextResponse, type NextRequest } from "next/server";
import {
  AdeniRoles,
  getRolesFromAuth0User,
  type AdeniRole,
} from "@adeni/shared";
import { auth0 } from "./auth0";
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
  if (!isAuth0Configured()) {
    return NextResponse.next();
  }

  const authResponse = await auth0.middleware(request);
  const { pathname } = request.nextUrl;

  if (pathname.startsWith("/auth")) {
    return authResponse;
  }

  const requiresBusiness = pathname.startsWith("/business");
  const requiresAdmin = pathname.startsWith("/admin");

  if (!requiresBusiness && !requiresAdmin) {
    return authResponse;
  }

  const session = await auth0.getSession(request);
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
