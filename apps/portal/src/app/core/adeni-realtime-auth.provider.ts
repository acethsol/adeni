import { Injector, type Provider } from "@angular/core";
import { type AdeniRealtimeAuth, ADENI_REALTIME_AUTH } from "@adeni/ui";
import { auth0FirstValueFrom, resolveAuthService } from "./auth0-rxjs";
import {
  ADENI_PORTAL_CONFIG,
  isAuth0Configured,
  isBusinessPortalDevMode,
  type AdeniPortalConfig,
} from "./adeni-config";

export function providePortalRealtimeAuth(): Provider {
  return {
    provide: ADENI_REALTIME_AUTH,
    deps: [ADENI_PORTAL_CONFIG, Injector],
    useFactory: (config: AdeniPortalConfig, injector: Injector): AdeniRealtimeAuth => ({
      getHubUrl(hub) {
        const base = `${config.apiBaseUrl}/hubs/${hub}`;
        if (!isAuth0Configured(config) && isBusinessPortalDevMode(config)) {
          const sub = encodeURIComponent(config.devBusinessAuth0Sub);
          return `${base}?dev_auth0_sub=${sub}`;
        }
        return base;
      },
      async getAccessToken() {
        if (!isAuth0Configured(config)) {
          return "";
        }
        const auth = resolveAuthService(injector);
        if (!auth) {
          return "";
        }
        return auth0FirstValueFrom<string>(
          auth.getAccessTokenSilently({
            authorizationParams: { audience: config.auth0.audience },
          }),
        );
      },
    }),
  };
}
