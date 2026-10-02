import { InjectionToken } from "@angular/core";
import { environment } from "../../environments/environment";

export type AdeniDiscoverConfig = {
  production: boolean;
  apiBaseUrl: string;
  defaultMarketId: string;
  defaultLocation: { lat: number; lng: number };
};

export const ADENI_DISCOVER_CONFIG = new InjectionToken<AdeniDiscoverConfig>(
  "ADENI_DISCOVER_CONFIG",
);

export function adeniDiscoverConfigFactory(): AdeniDiscoverConfig {
  return {
    production: environment.production,
    apiBaseUrl: environment.apiBaseUrl.replace(/\/$/, ""),
    defaultMarketId: environment.defaultMarketId,
    defaultLocation: environment.defaultLocation,
  };
}
