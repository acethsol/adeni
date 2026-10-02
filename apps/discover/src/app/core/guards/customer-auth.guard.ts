import { inject } from "@angular/core";
import { CanActivateFn, Router } from "@angular/router";
import {
  ADENI_DISCOVER_CONFIG,
  isAuth0Configured,
  isDiscoverCustomerDevMode,
} from "../adeni-config";
import { CustomerApiService } from "../services/customer-api.service";

/** Requires Auth0 customer session, or local dev customer sub. */
export const customerAuthGuard: CanActivateFn = async () => {
  const config = inject(ADENI_DISCOVER_CONFIG);
  const router = inject(Router);
  const api = inject(CustomerApiService);

  if (isDiscoverCustomerDevMode(config)) {
    return true;
  }

  if (!isAuth0Configured(config)) {
    return router.createUrlTree(["/discover"]);
  }

  if (await api.isLoggedIn()) {
    return true;
  }

  await api.login("/my-bookings");
  return false;
};
