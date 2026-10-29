import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { PortalSessionService } from "../../core/services/portal-session.service";
/** Fixed custom charges do not depend on the booking deposit capability. Server roles remain authoritative. */
export const commercePermissionGuard: CanActivateFn = async () => {
  const session=inject(PortalSessionService);const router=inject(Router);await session.ensureLoaded();
  return session.can("business.finance") || router.createUrlTree(["/forbidden"]);
};
