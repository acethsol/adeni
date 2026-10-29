import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import { AuthService } from "@auth0/auth0-angular";
import { AdeniRoles, userHasRole } from "@adeni/shared";
import { auth0FirstValueFrom } from "../auth0-rxjs";
import {
  ADENI_PORTAL_CONFIG,
  isAuth0Configured,
  isBusinessPortalDevMode,
} from "../adeni-config";
import { isPortalDevSignedOut } from "../portal-dev-session";

/** Auth0 login + business role, or local dev sub bypass. */
export const businessAuthGuard: CanActivateFn = async () => {
  const config = inject(ADENI_PORTAL_CONFIG);
  const router = inject(Router);

  if (isBusinessPortalDevMode(config)) {
    return isPortalDevSignedOut() ? router.createUrlTree(["/setup"]) : true;
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
  if (!userHasRole(user as Record<string, unknown>, AdeniRoles.Business)) {
    return router.createUrlTree(["/forbidden"]);
  }

  return true;
};
