import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { ADENI_ADMIN_CONFIG, canAccessAdminPortal } from "../adeni-config";

export const adminAccessGuard: CanActivateFn = () => {
  const config = inject(ADENI_ADMIN_CONFIG);
  const router = inject(Router);

  if (canAccessAdminPortal(config)) {
    return true;
  }

  return router.createUrlTree(["/setup"]);
};
