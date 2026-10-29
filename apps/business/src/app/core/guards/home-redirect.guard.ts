import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { BusinessSessionService } from "../services/business-session.service";

/** Practitioners land on their own calendar; everyone else on the dashboard. */
export const homeRedirectGuard: CanActivateFn = async () => {
  const session = inject(BusinessSessionService);
  const router = inject(Router);

  await session.ensureLoaded();

  if (
    session.can("business.staff.self") &&
    !session.can("business.staff") &&
    session.staffMemberId()
  ) {
    return router.createUrlTree(["/my-calendar"]);
  }

  return router.createUrlTree(["/dashboard"]);
};
