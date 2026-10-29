import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { PortalSessionService } from "../services/portal-session.service";

/**
 * Roster admins can open any staff calendar; practitioners only their own.
 */
export const staffCalendarGuard: CanActivateFn = async (route) => {
  const session = inject(PortalSessionService);
  const router = inject(Router);

  await session.ensureLoaded();

  if (session.can("portal.staff")) {
    return true;
  }

  const id = route.paramMap.get("id");
  if (session.can("portal.staff.self") && id && id === session.staffMemberId()) {
    return true;
  }

  if (session.can("portal.staff.self") && session.staffMemberId()) {
    return router.createUrlTree(["/my-calendar"]);
  }

  if (session.can("portal.overview")) {
    return router.createUrlTree(["/dashboard"]);
  }

  return router.createUrlTree(["/forbidden"]);
};
