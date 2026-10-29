import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { PortalSessionService } from "../../core/services/portal-session.service";
export const calendarPermissionGuard: CanActivateFn = async () => {
  const session=inject(PortalSessionService); const router=inject(Router); await session.ensureLoaded();
  return (session.can("business.staff") && ["owner","manager"].includes(session.permissionRole())) || router.createUrlTree(["/forbidden"]);
};
