import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import {
  ADENI_DISCOVER_CONFIG,
  isAuth0Configured,
  isDiscoverCustomerDevMode,
} from "../adeni-config";
import { CUSTOMER_APP_HOME } from "../../app-surface/customer-nav";
import { CustomerApiService } from "../services/customer-api.service";
import { AuthReturnService } from "../services/auth-return.service";

/**
 * Requires Auth0 customer session, or local dev customer sub (Development/Testing only).
 * Applied once on the `/app` parent route; the requested URL is the login return target,
 * validated by {@link AuthReturnService} (same-origin allowlist).
 */
export const customerAuthGuard: CanActivateFn = async (_route, state) => {
  const config = inject(ADENI_DISCOVER_CONFIG);
  const router = inject(Router);
  const api = inject(CustomerApiService);
  const authReturn = inject(AuthReturnService);

  if (isDiscoverCustomerDevMode(config)) {
    return true;
  }

  if (!isAuth0Configured(config)) {
    return router.createUrlTree(["/discover"]);
  }

  if (await api.isLoggedIn()) {
    return true;
  }

  await api.login(authReturn.resolveTarget(state.url, CUSTOMER_APP_HOME));
  return false;
};
