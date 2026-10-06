import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { AuthService } from "@auth0/auth0-angular";
import { AdeniRoles, userHasRole } from "@adeni/shared";
import { auth0FirstValueFrom } from "../auth0-rxjs";
import {
  ADENI_ADMIN_CONFIG,
  isAdminPortalDevMode,
  isAuth0Configured,
} from "../adeni-config";
import { isAdminDevSignedOut } from "../admin-dev-session";

export const adminAuthGuard: CanActivateFn = async () => {
  const config = inject(ADENI_ADMIN_CONFIG);
  const router = inject(Router);

  if (isAdminPortalDevMode(config)) {
    return isAdminDevSignedOut() ? router.createUrlTree(["/setup"]) : true;
  }

  if (!isAuth0Configured(config)) {
    return router.createUrlTree(["/setup"]);
  }

  const auth = inject(AuthService);
  const loggedIn = await auth0FirstValueFrom(auth.isAuthenticated$);
  if (!loggedIn) {
    await auth0FirstValueFrom(
      auth.loginWithRedirect({
        appState: { target: window.location.pathname },
      }),
    );
    return false;
  }

  const user = await auth0FirstValueFrom(auth.user$);
  if (!userHasRole(user as Record<string, unknown>, AdeniRoles.Admin)) {
    return router.createUrlTree(["/forbidden"]);
  }

  return true;
};
