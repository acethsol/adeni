import { InjectionToken } from "@angular/core";
import { environment } from "../../environments/environment";

export type AdeniDiscoverConfig = {
  production: boolean;
  apiBaseUrl: string;
  /** Canonical public origin for SEO (e.g. http://localhost:5190). */
  publicAppUrl: string;
  /** Business portal origin. Empty hides “List your business” until a host is set. */
  portalAppUrl: string;
  /** Optional ops override — mirrors `ADENI_MARKET` / `NEXT_PUBLIC_ADENI_MARKET`. */
  envMarketId: string;
  defaultMarketId: string;
  defaultLocation: { lat: number; lng: number };
  devCustomerAuth0Sub: string;
  auth0: {
    domain: string;
    clientId: string;
    audience: string;
  };
};

export const ADENI_DISCOVER_CONFIG = new InjectionToken<AdeniDiscoverConfig>(
  "ADENI_DISCOVER_CONFIG",
);

export function adeniDiscoverConfigFactory(): AdeniDiscoverConfig {
  return {
    production: environment.production,
    apiBaseUrl: environment.apiBaseUrl.replace(/\/$/, ""),
    publicAppUrl: environment.publicAppUrl.replace(/\/$/, ""),
    portalAppUrl: environment.portalAppUrl.replace(/\/$/, ""),
    envMarketId: environment.envMarketId.trim(),
    defaultMarketId: environment.defaultMarketId,
    defaultLocation: environment.defaultLocation,
    devCustomerAuth0Sub: environment.devCustomerAuth0Sub.trim(),
    auth0: {
      domain: environment.auth0.domain.trim(),
      clientId: environment.auth0.clientId.trim(),
      audience: environment.auth0.audience.trim(),
    },
  };
}

export function isAuth0Configured(config: AdeniDiscoverConfig): boolean {
  return Boolean(config.auth0.domain && config.auth0.clientId);
}

export function isDiscoverCustomerDevMode(config: AdeniDiscoverConfig): boolean {
  return !isAuth0Configured(config) && Boolean(config.devCustomerAuth0Sub);
}

export function isCustomerBookingEnabled(config: AdeniDiscoverConfig): boolean {
  return isAuth0Configured(config) || isDiscoverCustomerDevMode(config);
}
