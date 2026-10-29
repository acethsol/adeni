import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { PortalSessionService } from "../services/portal-session.service";

/** Practitioners land on their own calendar; everyone else on the dashboard. */
export const homeRedirectGuard: CanActivateFn = async () => {
  const session = inject(PortalSessionService);
  const router = inject(Router);

  await session.ensureLoaded();

  if (
    session.can("portal.staff.self") &&
    !session.can("portal.staff") &&
    session.staffMemberId()
  ) {
    return router.createUrlTree(["/my-calendar"]);
  }

  return router.createUrlTree(["/dashboard"]);
};
