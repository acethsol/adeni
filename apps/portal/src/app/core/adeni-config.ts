import { InjectionToken } from "@angular/core";
import { environment } from "../../environments/environment";

export type AdeniPortalConfig = {
  production: boolean;
  apiBaseUrl: string;
  discoverWebUrl: string;
  devBusinessAuth0Sub: string;
  /** Dev Auth0 sub used when accepting a staff invite (must not be the owner sub). */
  devStaffAuth0Sub: string;
  auth0: {
    domain: string;
    clientId: string;
    audience: string;
  };
};

export const ADENI_PORTAL_CONFIG = new InjectionToken<AdeniPortalConfig>("ADENI_PORTAL_CONFIG");

export function adeniPortalConfigFactory(): AdeniPortalConfig {
  return {
    production: environment.production,
    apiBaseUrl: environment.apiBaseUrl.replace(/\/$/, ""),
    discoverWebUrl: environment.discoverWebUrl.replace(/\/$/, ""),
    devBusinessAuth0Sub: environment.devBusinessAuth0Sub.trim(),
    devStaffAuth0Sub: (environment.devStaffAuth0Sub ?? "auth0|local-staff").trim(),
    auth0: {
      domain: environment.auth0.domain.trim(),
      clientId: environment.auth0.clientId.trim(),
      audience: environment.auth0.audience.trim(),
    },
  };
}

export function isAuth0Configured(config: AdeniPortalConfig): boolean {
  return Boolean(config.auth0.domain && config.auth0.clientId);
}

export function isBusinessPortalDevMode(config: AdeniPortalConfig): boolean {
  return !isAuth0Configured(config) && Boolean(config.devBusinessAuth0Sub);
}

export function canAccessBusinessPortal(config: AdeniPortalConfig): boolean {
  return isAuth0Configured(config) || isBusinessPortalDevMode(config);
}
