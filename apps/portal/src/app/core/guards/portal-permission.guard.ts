import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import type { PortalPermission } from "@adeni/shared";
import { findPortalNavItem } from "../portal-nav";
import { PortalSessionService } from "../services/portal-session.service";

/** Blocks routes the current portal RBAC role cannot open. */
export const portalPermissionGuard: CanActivateFn = async (route) => {
  const session = inject(PortalSessionService);
  const router = inject(Router);

  await session.ensureLoaded();

  const path = "/" + (route.routeConfig?.path ?? "").split("/:")[0];
  const match = findPortalNavItem(path === "/" ? "/dashboard" : path);
  const required = match?.item.permission as PortalPermission | readonly PortalPermission[] | undefined;

  if (session.can(required)) {
    return true;
  }

  // Practitioners land on overview when deep-linking to a forbidden page.
  if (session.can("portal.overview")) {
    return router.createUrlTree(["/dashboard"]);
  }

  return router.createUrlTree(["/forbidden"]);
};
