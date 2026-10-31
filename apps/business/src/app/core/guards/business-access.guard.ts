import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import {
  ADENI_PORTAL_CONFIG,
  canAccessBusinessPortal,
} from "../adeni-config";

/** Sends users to /welcome when Auth0 and dev sub are both unset. */
export const businessAccessGuard: CanActivateFn = () => {
  const config = inject(ADENI_PORTAL_CONFIG);
  const router = inject(Router);

  if (canAccessBusinessPortal(config)) {
    return true;
  }

  return router.createUrlTree(["/welcome"]);
};
