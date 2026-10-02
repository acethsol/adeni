import { InjectionToken } from "@angular/core";
import { environment } from "../../environments/environment";

export type AdeniAdminConfig = {
  production: boolean;
  apiBaseUrl: string;
  discoverWebUrl: string;
  devAdminAuth0Sub: string;
  auth0: {
    domain: string;
    clientId: string;
    audience: string;
  };
};

export const ADENI_ADMIN_CONFIG = new InjectionToken<AdeniAdminConfig>("ADENI_ADMIN_CONFIG");

export function adeniAdminConfigFactory(): AdeniAdminConfig {
  return {
    production: environment.production,
    apiBaseUrl: environment.apiBaseUrl.replace(/\/$/, ""),
    discoverWebUrl: environment.discoverWebUrl.replace(/\/$/, ""),
    devAdminAuth0Sub: environment.devAdminAuth0Sub.trim(),
    auth0: {
      domain: environment.auth0.domain.trim(),
      clientId: environment.auth0.clientId.trim(),
      audience: environment.auth0.audience.trim(),
    },
  };
}

export function isAuth0Configured(config: AdeniAdminConfig): boolean {
  return Boolean(config.auth0.domain && config.auth0.clientId);
}

export function isAdminPortalDevMode(config: AdeniAdminConfig): boolean {
  return !isAuth0Configured(config) && Boolean(config.devAdminAuth0Sub);
}

export function canAccessAdminPortal(config: AdeniAdminConfig): boolean {
  return isAuth0Configured(config) || isAdminPortalDevMode(config);
}
