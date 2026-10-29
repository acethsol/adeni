import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { BusinessSessionService } from "../../core/services/business-session.service";
export const calendarPermissionGuard: CanActivateFn = async () => {
  const session=inject(BusinessSessionService); const router=inject(Router); await session.ensureLoaded();
  return (session.can("business.staff") && ["owner","manager"].includes(session.permissionRole())) || router.createUrlTree(["/forbidden"]);
};
